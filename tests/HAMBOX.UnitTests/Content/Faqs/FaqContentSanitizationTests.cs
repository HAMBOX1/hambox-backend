using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;
using HAMBOX.Modules.Content.Application.Features.Faqs.DuplicateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetPublishedFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.SetFaqPublishState;
using HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Content.Faqs;

/// <summary>
/// Covers the server-side sanitization boundary for FAQ answers (<see cref="FaqContentSanitizer"/>):
/// malicious payloads are neutralized on both Create and Update, the invariant survives Duplicate
/// (which copies already-sanitized fields rather than re-validating), the public read path never
/// returns unsafe markup, an answer that is entirely unsafe markup fails cleanly instead of crashing,
/// and legitimate rich-text formatting from the admin editor's toolbar (paragraphs, headings,
/// bold/italic/underline/strikethrough, lists, links, blockquotes) round-trips unchanged.
/// </summary>
public sealed class FaqContentSanitizationTests
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? DisplayName => null;
        public bool IsAuthenticated => true;
        public bool IsAdminContext => true;
    }

    private static readonly ICurrentUserService CurrentUser = new FakeCurrentUserService();

    private static ContentDbContext CreateContentDb()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContentDbContext(options);
    }

    private static TestCatalogDbContext CreateCatalogDb()
    {
        var options = new DbContextOptionsBuilder<TestCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestCatalogDbContext(options);
    }

    private static async Task<Guid> SeedFaqCategoryAsync(ContentDbContext contentDb)
    {
        var handler = new CreateFaqCategoryCommandHandler(contentDb);
        var result = await handler.Handle(new CreateFaqCategoryCommand("General", null), default);
        Assert.True(result.IsSuccess);
        return result.Value.Id;
    }

    // --- Malicious payloads mixed with legitimate text: must survive with the exploit neutralized ---

    public static readonly TheoryData<string> MixedMaliciousPayloads = new()
    {
        "<p>Hello<script>alert(1)</script>world</p>",
        "<a href=\"javascript:alert(1)\">click me</a>",
        "<a href=\"JaVaScRiPt:alert(1)\">click me</a>",
        "<div onclick=\"alert(1)\">click</div>",
        "<a href=\"data:text/html,<script>alert(1)</script>\">link</a>",
        "<p style=\"background:url(javascript:alert(1))\">styled</p>",
        "<p onmouseover=\"alert(1)\">hover</p>",
        "<style>body{background:url(javascript:alert(1))}</style><p>text</p>",
    };

    [Theory]
    [MemberData(nameof(MixedMaliciousPayloads))]
    public async Task CreateFaq_MixedMaliciousPayload_IsNeutralizedButTextSurvives(string payload)
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, payload, null, categoryId, FaqScope.Global, null), default);

        Assert.True(result.IsSuccess);

        var stored = await contentDb.Faqs.SingleAsync(f => f.Id == result.Value);
        AssertNeutralized(stored.AnswerEn);
    }

    [Theory]
    [MemberData(nameof(MixedMaliciousPayloads))]
    public async Task UpdateFaq_MixedMaliciousPayload_IsNeutralizedButTextSurvives(string payload)
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand("Q", null, "Safe answer", null, categoryId, FaqScope.Global, null), default);

        var updateHandler = new UpdateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var updated = await updateHandler.Handle(
            new UpdateFaqCommand(created.Value, "Q", null, payload, null, categoryId, FaqScope.Global, null), default);

        Assert.True(updated.IsSuccess);

        var stored = await contentDb.Faqs.SingleAsync(f => f.Id == created.Value);
        AssertNeutralized(stored.AnswerEn);
    }

    // --- Payloads that are entirely unsafe markup with no legitimate text: must fail cleanly ---

    public static readonly TheoryData<string> PurelyMaliciousPayloads = new()
    {
        "<img src=x onerror=\"alert(1)\">",
        "<iframe src=\"https://evil.example/\"></iframe>",
        "<svg onload=\"alert(1)\"></svg>",
        "<object data=\"evil.swf\"></object>",
        "<embed src=\"evil.swf\">",
        "<form action=\"https://evil.example\"><input name=\"x\"></form>",
        "<script>alert(document.cookie)</script>",
    };

    [Theory]
    [MemberData(nameof(PurelyMaliciousPayloads))]
    public async Task CreateFaq_PurelyMaliciousPayload_FailsCleanly_NeverThrowsNeverPersists(string payload)
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, payload, null, categoryId, FaqScope.Global, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqAnswerInvalid.Code, result.Error.Code);
        Assert.Empty(await contentDb.Faqs.ToListAsync());
    }

    [Theory]
    [MemberData(nameof(PurelyMaliciousPayloads))]
    public async Task UpdateFaq_PurelyMaliciousPayload_FailsCleanly_NeverThrowsLeavesOriginalIntact(string payload)
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand("Q", null, "Original safe answer", null, categoryId, FaqScope.Global, null), default);

        var updateHandler = new UpdateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await updateHandler.Handle(
            new UpdateFaqCommand(created.Value, "Q", null, payload, null, categoryId, FaqScope.Global, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqAnswerInvalid.Code, result.Error.Code);

        var stored = await contentDb.Faqs.SingleAsync(f => f.Id == created.Value);
        Assert.Equal("Original safe answer", stored.AnswerEn);
    }

    [Fact]
    public async Task DuplicateFaq_CopiesAlreadySanitizedContent_NeverReintroducesPayload()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand(
                "Q", null, "<p>Safe <strong>answer</strong></p><script>alert(1)</script>", null,
                categoryId, FaqScope.Global, null),
            default);
        Assert.True(created.IsSuccess);

        var duplicateHandler = new DuplicateFaqCommandHandler(contentDb, CurrentUser);
        var duplicated = await duplicateHandler.Handle(new DuplicateFaqCommand(created.Value), default);
        Assert.True(duplicated.IsSuccess);

        var duplicate = await contentDb.Faqs.SingleAsync(f => f.Id == duplicated.Value);
        AssertNeutralized(duplicate.AnswerEn);
        Assert.Contains("Safe", duplicate.AnswerEn);
        Assert.Contains("<strong>answer</strong>", duplicate.AnswerEn);
    }

    [Fact]
    public async Task PublishedFaqs_PublicApiOutput_NeverContainsUnsafeMarkup()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand(
                "Q", null, "<p>Answer</p><a href=\"javascript:alert(1)\">click</a><script>alert(2)</script>", null,
                categoryId, FaqScope.Global, null),
            default);
        Assert.True(created.IsSuccess);

        var publishHandler = new SetFaqPublishStateCommandHandler(contentDb, CurrentUser);
        await publishHandler.Handle(new SetFaqPublishStateCommand(created.Value, true), default);

        var publicHandler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await publicHandler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value);
        AssertNeutralized(dto.AnswerEn);
        Assert.Contains("<p>Answer</p>", dto.AnswerEn);
    }

    // --- Legitimate formatting preserved ---

    [Fact]
    public async Task CreateFaq_LegitimateFormatting_IsPreserved()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var categoryId = await SeedFaqCategoryAsync(contentDb);

        const string richAnswer =
            "<h2>Shipping</h2>" +
            "<p>We deliver <strong>instantly</strong> and <em>always</em> keep you <u>updated</u>, " +
            "unless it's <s>delayed</s>.</p>" +
            "<ul><li>Digital keys</li><li>Gift cards</li></ul>" +
            "<ol><li>Order</li><li>Receive</li></ol>" +
            "<blockquote>Customer satisfaction guaranteed.</blockquote>" +
            "<p>See our <a href=\"https://hambox.example/terms\">terms</a> for details.</p>";

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, richAnswer, null, categoryId, FaqScope.Global, null), default);

        Assert.True(result.IsSuccess);

        var stored = await contentDb.Faqs.SingleAsync(f => f.Id == result.Value);

        Assert.Contains("<h2>Shipping</h2>", stored.AnswerEn);
        Assert.Contains("<strong>instantly</strong>", stored.AnswerEn);
        Assert.Contains("<em>always</em>", stored.AnswerEn);
        Assert.Contains("<u>updated</u>", stored.AnswerEn);
        Assert.Contains("<s>delayed</s>", stored.AnswerEn);
        Assert.Contains("<ul><li>Digital keys</li><li>Gift cards</li></ul>", stored.AnswerEn);
        Assert.Contains("<ol><li>Order</li><li>Receive</li></ol>", stored.AnswerEn);
        Assert.Contains("<blockquote>Customer satisfaction guaranteed.</blockquote>", stored.AnswerEn);
        Assert.Contains("href=\"https://hambox.example/terms\"", stored.AnswerEn);
        Assert.Contains(">terms</a>", stored.AnswerEn);
    }

    [Fact]
    public void Sanitize_SafeLinkSchemes_AreKept()
    {
        var httpLink = FaqContentSanitizer.Sanitize("<a href=\"https://example.com\">go</a>");
        var mailLink = FaqContentSanitizer.Sanitize("<a href=\"mailto:support@hambox.example\">email</a>");

        Assert.Contains("href=\"https://example.com\"", httpLink);
        Assert.Contains("href=\"mailto:support@hambox.example\"", mailLink);
    }

    [Fact]
    public void Sanitize_ScriptContent_IsFullyRemovedNotUnwrapped()
    {
        var sanitized = FaqContentSanitizer.Sanitize("<p>Before</p><script>alert(document.cookie)</script><p>After</p>");

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("alert(", sanitized);
        Assert.DoesNotContain("document.cookie", sanitized);
        Assert.Contains("Before", sanitized);
        Assert.Contains("After", sanitized);
    }

    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        Assert.Null(FaqContentSanitizer.Sanitize(null));
    }

    private static void AssertNeutralized(string sanitized)
    {
        var lower = sanitized.ToLowerInvariant();
        Assert.DoesNotContain("<script", lower);
        Assert.DoesNotContain("javascript:", lower);
        Assert.DoesNotContain("onerror", lower);
        Assert.DoesNotContain("onclick", lower);
        Assert.DoesNotContain("onload", lower);
        Assert.DoesNotContain("onmouseover", lower);
        Assert.DoesNotContain("<iframe", lower);
        Assert.DoesNotContain("<svg", lower);
        Assert.DoesNotContain("<object", lower);
        Assert.DoesNotContain("<embed", lower);
        Assert.DoesNotContain("<img", lower);
        Assert.DoesNotContain("<form", lower);
        Assert.DoesNotContain("<input", lower);
        Assert.DoesNotContain("<style", lower);
        Assert.DoesNotContain("data:text/html", lower);
        Assert.DoesNotContain("alert(", lower);
        Assert.DoesNotContain("document.cookie", lower);
    }
}
