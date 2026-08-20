using System.Globalization;
using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.GetMyAlertSubscriptions;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.RemoveAlertSubscription;
using HAMBOX.Modules.Commerce.Application.Features.Account.Orders.GetOrders;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Application.Services;

/// <summary>
/// Menu-driven WhatsApp bot engine — no AI/NLP anywhere. Every inbound message is either a menu number
/// or a single piece of free text the current state is explicitly waiting for (an email, a search term,
/// a support message). All real data comes from existing Catalog/Commerce/Support MediatR
/// queries/commands via <see cref="ISender"/>; this class only owns menu navigation and reply text.
/// </summary>
public sealed class WhatsAppBotEngine(
    IMessagingDbContext messagingDb,
    ISender sender,
    IWhatsAppProvider provider,
    WhatsAppLinkVerificationService linkVerification,
    IWhatsAppUserContextScope userContextScope,
    IPlatformSettingsProvider platformSettings,
    IWhatsAppBotConfigurationProvider menuConfigProvider) : IWhatsAppBotEngine
{
    private static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(30);
    private const int PageSize = 8;

    // Generous for any real product-name search, but small enough that Term plus the rest of
    // MenuContext (Ids, Page, ReturnState) can never approach ContextJson's 2000-char column limit —
    // a legal WhatsApp message can be up to 4096 chars, which alone would overflow that column and
    // throw a truncation DbUpdateException from inside SaveChangesAsync.
    private const int MaxSearchTermLength = 200;

    // Loaded once per inbound message (see HandleInboundMessageAsync) and read by every Render* call in
    // this instance — WhatsAppBotEngine is registered Scoped (one instance per webhook request), so a
    // plain field is safe here without any request-scoped threading through every method signature.
    // Purely presentation: menu text and which action a numbered choice maps to. Never consulted for
    // authorization — EnterGatedAsync's IsLinked check and every downstream Catalog/Commerce/Support
    // call are completely unaffected by what this holds.
    private WhatsAppBotMenuSnapshot _menuConfig = WhatsAppBotConfigurationDefaults.Snapshot;

    public async Task HandleInboundMessageAsync(WhatsAppInboundMessage message, CancellationToken cancellationToken)
    {
        _menuConfig = await menuConfigProvider.GetAsync(cancellationToken);

        var phoneNumber = message.FromPhoneNumber.Trim();
        var session = await messagingDb.WhatsAppConversationSessions
            .FirstOrDefaultAsync(s => s.PhoneNumber == phoneNumber, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var isNew = session is null;

        if (session is null)
        {
            session = WhatsAppConversationSession.CreateNew(phoneNumber, SessionWindow);
            messagingDb.WhatsAppConversationSessions.Add(session);
        }
        else if (session.IsExpired(now))
        {
            session.ResetToMain();
            // A long-idle session's account link must not persist indefinitely — otherwise anyone who
            // later controls this phone number (or spoofs it, since the webhook has no caller
            // authentication yet — see the endpoint's doc comment) inherits privileged access without
            // ever proving account ownership themselves. Re-linking (email + code) is required again.
            session.Unlink();
        }

        session.Touch(SessionWindow, now);

        var input = (message.Text ?? string.Empty).Trim();

        using var impersonation = session.IsLinked ? userContextScope.ActAsCustomer(session.CustomerUserId!) : null;

        var reply = isNew
            ? RenderLanguagePrompt(showCancel: false)
            : await DispatchAsync(session, input, cancellationToken);

        await messagingDb.SaveChangesAsync(cancellationToken);
        await provider.SendTextMessageAsync(phoneNumber, reply, cancellationToken);
    }

    private async Task<string> DispatchAsync(WhatsAppConversationSession session, string input, CancellationToken ct) =>
        session.CurrentMenu switch
        {
            WhatsAppMenuState.LanguageSelect => HandleLanguageSelect(session, input),
            WhatsAppMenuState.Main => await HandleMainAsync(session, input, ct),
            WhatsAppMenuState.BrowseCategories => await HandleBrowseCategoriesAsync(session, input, ct),
            WhatsAppMenuState.BrowseProducts => await HandleBrowseProductsAsync(session, input, ct),
            WhatsAppMenuState.ProductDetail => await HandleProductDetailAsync(session, input, ct),
            WhatsAppMenuState.VariantSelect => await HandleVariantSelectAsync(session, input, ct),
            WhatsAppMenuState.SearchPrompt => await HandleSearchPromptAsync(session, input, ct),
            WhatsAppMenuState.SearchResults => await HandleSearchResultsAsync(session, input, ct),
            WhatsAppMenuState.Cart => await HandleCartAsync(session, input, ct),
            WhatsAppMenuState.LinkEmailPrompt => await HandleLinkEmailPromptAsync(session, input, ct),
            WhatsAppMenuState.LinkCodePrompt => await HandleLinkCodePromptAsync(session, input, ct),
            WhatsAppMenuState.Orders => await HandleOrdersAsync(session, input, ct),
            WhatsAppMenuState.Alerts => await HandleAlertsAsync(session, input, ct),
            WhatsAppMenuState.SupportMenu => await HandleSupportMenuAsync(session, input, ct),
            WhatsAppMenuState.SupportCompose => await HandleSupportComposeAsync(session, input, ct),
            _ => await RenderMainAsync(session),
        };

    // ── Language ─────────────────────────────────────────────────────────────

    private static string RenderLanguagePrompt(bool showCancel) =>
        "Welcome to HAMBOX! Please choose your language / يرجى اختيار لغتك:\n1. English\n2. العربية"
            + (showCancel ? "\n0. Cancel" : string.Empty);

    private string HandleLanguageSelect(WhatsAppConversationSession session, string input)
    {
        // "0 to cancel" only makes sense once there's a menu to go back to — a brand-new session has
        // no prior state, so its very first prompt (isNew short-circuits before this ever runs) never
        // offers it; every subsequent re-prompt (invalid input, or switching language from Main) does.
        switch (input)
        {
            case "1":
                session.SetLanguage("en");
                break;
            case "2":
                session.SetLanguage("ar");
                break;
            case "0":
                break; // keep the current language, fall through to Main unchanged
            default:
                return RenderLanguagePrompt(showCancel: true);
        }

        session.ResetToMain();
        return RenderMain(session);
    }

    // ── Main menu ────────────────────────────────────────────────────────────

    private Task<string> HandleMainAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        var items = _menuConfig.EnabledItemsInOrder;
        if (int.TryParse(input, out var index) && index >= 1 && index <= items.Count)
        {
            return DispatchMenuActionAsync(items[index - 1].Action, session, ct);
        }

        return Task.FromResult(RenderMain(session, invalid: true));
    }

    /// <summary>Maps a configured menu item's fixed <see cref="WhatsAppMenuAction"/> to the same
    /// handler method the old hardcoded "1".."7" switch called — only the number-to-action mapping is
    /// now data-driven; every destination, and everything it does once reached, is unchanged.</summary>
    private Task<string> DispatchMenuActionAsync(WhatsAppMenuAction action, WhatsAppConversationSession session, CancellationToken ct) =>
        action switch
        {
            WhatsAppMenuAction.BrowseProducts => EnterBrowseCategoriesAsync(session, ct),
            WhatsAppMenuAction.SearchProducts => Task.FromResult(EnterSearchPrompt(session)),
            WhatsAppMenuAction.Cart => EnterCartAsync(session, ct),
            WhatsAppMenuAction.Orders => EnterGatedAsync(session, WhatsAppMenuState.Orders, ct),
            WhatsAppMenuAction.Alerts => EnterGatedAsync(session, WhatsAppMenuState.Alerts, ct),
            WhatsAppMenuAction.Support => EnterGatedAsync(session, WhatsAppMenuState.SupportMenu, ct),
            WhatsAppMenuAction.Language => Task.FromResult(EnterLanguageChange(session)),
            _ => Task.FromResult(RenderMain(session, invalid: true)),
        };

    private string RenderMain(WhatsAppConversationSession session, bool invalid = false)
    {
        session.SetMenu(WhatsAppMenuState.Main);
        var config = _menuConfig;

        var prefix = invalid ? L(session, config.FallbackMessageEn, config.FallbackMessageAr) + "\n\n" : string.Empty;
        var welcome = L(session, config.WelcomeMessageEn, config.WelcomeMessageAr);
        var linkLine = session.IsLinked
            ? string.Empty
            : L(session, "\n(Orders/Alerts/Support need a quick account link.)", "\n(الطلبات/التنبيهات/الدعم تتطلب ربط الحساب أولاً.)");

        var lines = config.EnabledItemsInOrder.Select((item, i) => $"{i + 1}. {L(session, item.LabelEn, item.LabelAr)}");

        return prefix + welcome + "\n" + string.Join('\n', lines) + linkLine;
    }

    private Task<string> RenderMainAsync(WhatsAppConversationSession session) => Task.FromResult(RenderMain(session));

    private string EnterLanguageChange(WhatsAppConversationSession session)
    {
        session.SetMenu(WhatsAppMenuState.LanguageSelect);
        return RenderLanguagePrompt(showCancel: true);
    }

    /// <summary>Menu items 4/5/6 all funnel through here: linked sessions go straight to the target
    /// state, unlinked ones are parked at <see cref="WhatsAppMenuState.LinkEmailPrompt"/> with the
    /// target remembered in <see cref="MenuContext.ReturnState"/> so linking resumes exactly where the
    /// user asked to go.</summary>
    private Task<string> EnterGatedAsync(WhatsAppConversationSession session, string targetState, CancellationToken ct)
    {
        if (session.IsLinked)
        {
            return targetState switch
            {
                WhatsAppMenuState.Orders => RenderOrdersAsync(session, ct),
                WhatsAppMenuState.Alerts => RenderAlertsAsync(session, ct),
                WhatsAppMenuState.SupportMenu => Task.FromResult(RenderSupportMenu(session)),
                _ => RenderMainAsync(session),
            };
        }

        session.SetMenu(WhatsAppMenuState.LinkEmailPrompt);
        SetContext(session, new MenuContext(ReturnState: targetState));
        return Task.FromResult(L(session,
            "This needs your HAMBOX account. Please reply with the email on your account (or 0 to cancel).",
            "هذا يتطلب حسابك في HAMBOX. يرجى الرد بالبريد الإلكتروني الخاص بحسابك (أو 0 للإلغاء)."));
    }

    // ── Account linking ─────────────────────────────────────────────────────

    private async Task<string> HandleLinkEmailPromptAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            return RenderMain(session);
        }

        if (!input.Contains('@'))
        {
            return L(session, "That doesn't look like an email. Please try again (or 0 to cancel).",
                "هذا لا يبدو بريدًا إلكترونيًا صحيحًا. حاول مرة أخرى (أو 0 للإلغاء).");
        }

        var result = await linkVerification.StartVerificationAsync(session, input, ct);
        if (result == WhatsAppLinkStartResult.NoAccountFound)
        {
            return L(session, "No HAMBOX account found with that email. Try another email (or 0 to cancel).",
                "لم يتم العثور على حساب HAMBOX بهذا البريد الإلكتروني. جرّب بريدًا آخر (أو 0 للإلغاء).");
        }

        session.SetMenu(WhatsAppMenuState.LinkCodePrompt);
        return L(session, "We sent a 6-digit code to your email. Reply with the code (valid 10 minutes, or 0 to cancel).",
            "أرسلنا رمزًا مكونًا من 6 أرقام إلى بريدك الإلكتروني. أرسل الرمز (صالح لمدة 10 دقائق، أو 0 للإلغاء).");
    }

    private async Task<string> HandleLinkCodePromptAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            session.ClearVerification();
            return RenderMain(session);
        }

        var result = await linkVerification.ConfirmAsync(session, input, ct);
        switch (result)
        {
            case WhatsAppLinkConfirmResult.Linked:
                var context = GetContext(session);
                var target = context.ReturnState ?? WhatsAppMenuState.Main;
                using (var scope = userContextScope.ActAsCustomer(session.CustomerUserId!))
                {
                    var welcome = L(session, "Account linked! ", "تم ربط الحساب! ");
                    return welcome + target switch
                    {
                        WhatsAppMenuState.Orders => await RenderOrdersAsync(session, ct),
                        WhatsAppMenuState.Alerts => await RenderAlertsAsync(session, ct),
                        WhatsAppMenuState.SupportMenu => RenderSupportMenu(session),
                        _ => RenderMain(session),
                    };
                }
            case WhatsAppLinkConfirmResult.Expired:
                session.SetMenu(WhatsAppMenuState.LinkEmailPrompt);
                return L(session, "That code expired. Please reply with your email again.",
                    "انتهت صلاحية الرمز. يرجى إرسال بريدك الإلكتروني مرة أخرى.");
            case WhatsAppLinkConfirmResult.TooManyAttempts:
                session.SetMenu(WhatsAppMenuState.LinkEmailPrompt);
                return L(session, "Too many incorrect attempts. Please reply with your email again.",
                    "عدد كبير جدًا من المحاولات الخاطئة. يرجى إرسال بريدك الإلكتروني مرة أخرى.");
            case WhatsAppLinkConfirmResult.NoPendingVerification:
                session.SetMenu(WhatsAppMenuState.Main);
                return RenderMain(session);
            default:
                return L(session, "That code doesn't match. Please try again (or 0 to cancel).",
                    "الرمز غير مطابق. حاول مرة أخرى (أو 0 للإلغاء).");
        }
    }

    // ── Browse: categories → products → detail → variant ───────────────────

    private async Task<string> EnterBrowseCategoriesAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        session.SetMenu(WhatsAppMenuState.BrowseCategories);
        return await RenderCategoriesAsync(session, ct);
    }

    private async Task<string> RenderCategoriesAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        var result = await sender.Send(new GetCategoryTreeQuery(), ct);
        if (!result.IsSuccess)
        {
            return RenderError(session);
        }

        var categories = result.Value
            .Where(c => c.ParentId is null && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Take(20)
            .ToList();

        if (categories.Count == 0)
        {
            return L(session, "No categories are available right now. 0. Main Menu",
                "لا توجد فئات متاحة حاليًا. 0. القائمة الرئيسية");
        }

        SetContext(session, new MenuContext(Ids: categories.Select(c => c.Id.ToString()).ToList()));

        var lines = categories.Select((c, i) => $"{i + 1}. {(session.LanguageCode == "ar" ? c.NameAr : c.NameEn)}");
        var header = L(session, "Categories:\n", "الفئات:\n");
        var footer = L(session, "\n0. Main Menu", "\n0. القائمة الرئيسية");
        return header + string.Join('\n', lines) + footer;
    }

    private async Task<string> HandleBrowseCategoriesAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            return RenderMain(session);
        }

        var context = GetContext(session);
        if (!TryResolveListGuid(context, input, out var categoryId) || categoryId is null)
        {
            var listing = await RenderCategoriesAsync(session, ct);
            return L(session, "Please pick a number from the list.\n\n", "يرجى اختيار رقم من القائمة.\n\n") + listing;
        }

        session.SetSelectedCategory(categoryId);
        session.SetMenu(WhatsAppMenuState.BrowseProducts);
        SetContext(session, new MenuContext(Page: 1));
        return await RenderProductsAsync(session, ct, term: null);
    }

    private async Task<string> RenderProductsAsync(WhatsAppConversationSession session, CancellationToken ct, string? term)
    {
        var context = GetContext(session);
        var page = Math.Max(1, context.Page);

        var query = term is null
            ? new GetProductsQuery(page, PageSize, null, ProductStatus.Active, session.SelectedCategoryId, ProductSortBy.Newest)
            : new GetProductsQuery(page, PageSize, term, ProductStatus.Active, null, null);

        var result = await sender.Send(query, ct);
        if (!result.IsSuccess)
        {
            return RenderError(session);
        }

        var page1 = result.Value;
        if (page1.Items.Count == 0)
        {
            if (page == 1)
            {
                return L(session, "No games found. 0. Main Menu", "لم يتم العثور على ألعاب. 0. القائمة الرئيسية");
            }

            // Paged past the last real page (e.g. repeated "9") — revert to the last known-good page
            // instead of persisting an empty one, which would otherwise strand the context with no
            // selectable items and no way back except "0".
            SetContext(session, context with { Page = Math.Max(1, page - 1) });
            return L(session, "No more results.\n\n", "لا مزيد من النتائج.\n\n") + await RenderProductsAsync(session, ct, term);
        }

        SetContext(session, context with
        {
            Ids = page1.Items.Select(p => p.Id.ToString()).ToList(),
            Term = term,
            Page = page,
        });

        var lines = page1.Items.Select((p, i) =>
            $"{i + 1}. {(session.LanguageCode == "ar" ? p.NameAr : p.NameEn)} — ${p.Price.ToString("0.00", CultureInfo.InvariantCulture)}");

        var hasNext = page * PageSize < page1.TotalCount;
        var nav = new List<string>();
        if (hasNext)
        {
            nav.Add(L(session, "9. Next Page", "9. الصفحة التالية"));
        }
        if (page > 1)
        {
            nav.Add(L(session, "8. Previous Page", "8. الصفحة السابقة"));
        }
        nav.Add(L(session, "0. Back", "0. رجوع"));

        var header = L(session, "Games:\n", "الألعاب:\n");
        return header + string.Join('\n', lines) + "\n" + string.Join('\n', nav);
    }

    private async Task<string> HandleBrowseProductsAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        var context = GetContext(session);

        if (input == "0")
        {
            session.SetMenu(WhatsAppMenuState.BrowseCategories);
            return await RenderCategoriesAsync(session, ct);
        }

        if (input == "9" || input == "8")
        {
            var delta = input == "9" ? 1 : -1;
            SetContext(session, context with { Page = Math.Max(1, context.Page + delta) });
            return await RenderProductsAsync(session, ct, context.Term);
        }

        if (!TryResolveListGuid(context, input, out var productId) || productId is null)
        {
            return L(session, "Please pick a number from the list.\n\n", "يرجى اختيار رقم من القائمة.\n\n")
                + await RenderProductsAsync(session, ct, context.Term);
        }

        session.SetSelectedProduct(productId);
        session.SetMenu(WhatsAppMenuState.ProductDetail);
        return await RenderProductDetailAsync(session, ct);
    }

    private async Task<string> RenderProductDetailAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        var productId = session.SelectedProductId!.Value;

        var productResult = await sender.Send(new GetProductByIdQuery(productId), ct);
        var configResult = await sender.Send(new GetStorefrontProductConfigurationsQuery([productId]), ct);

        if (!productResult.IsSuccess)
        {
            return RenderError(session);
        }

        var product = productResult.Value;
        var config = configResult.IsSuccess ? configResult.Value.FirstOrDefault() : null;

        var name = session.LanguageCode == "ar" ? product.NameAr : product.NameEn;
        var description = session.LanguageCode == "ar" ? product.DescriptionAr : product.DescriptionEn;
        var shortDescription = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : (description.Length > 200 ? description[..200] + "…" : description) + "\n\n";

        var options = BuildVariantOptionLines(config, product);
        SetContext(session, new MenuContext(Ids: options.Ids));

        var header = $"{name}\n{shortDescription}" + L(session, "Choose an option:\n", "اختر خيارًا:\n");
        var footer = L(session, "\n0. Back to Games", "\n0. رجوع إلى الألعاب");
        return header + string.Join('\n', options.Lines) + footer;
    }

    private static (List<string> Ids, List<string> Lines) BuildVariantOptionLines(
        StorefrontProductConfigurationDto? config, ProductDto product)
    {
        if (config is null || config.Variants.Count == 0)
        {
            // No real variants — one implicit "Standard" option keyed by the empty-string sentinel so
            // the same numbered-selection code path handles both variant-backed and simple products.
            return ([string.Empty], [$"1. Standard — ${product.Price.ToString("0.00", CultureInfo.InvariantCulture)}"]);
        }

        var ids = new List<string>();
        var lines = new List<string>();
        var i = 1;
        foreach (var variant in config.Variants)
        {
            ids.Add(variant.Id.ToString());
            var stockLabel = variant.IsOutOfStock ? " (out of stock)" : variant.IsLowStock ? " (low stock)" : string.Empty;
            lines.Add($"{i}. {variant.Sku} — ${variant.Price.ToString("0.00", CultureInfo.InvariantCulture)}{stockLabel}");
            i++;
        }

        return (ids, lines);
    }

    private async Task<string> HandleProductDetailAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            session.SetMenu(WhatsAppMenuState.BrowseProducts);
            var context = GetContext(session);
            return await RenderProductsAsync(session, ct, context.Term);
        }

        var ctx = GetContext(session);
        if (!TryResolveListIndex(ctx, input, out var variantIdText) || variantIdText is null)
        {
            return L(session, "Please pick a number from the list.\n\n", "يرجى اختيار رقم من القائمة.\n\n")
                + await RenderProductDetailAsync(session, ct);
        }

        // TryParse, not Parse — see TryResolveListGuid: variantIdText is either the empty "Standard"
        // sentinel or a value this instance wrote itself, but a corrupted row must fail safely.
        if (variantIdText.Length > 0 && !Guid.TryParse(variantIdText, out _))
        {
            return L(session, "Please pick a number from the list.\n\n", "يرجى اختيار رقم من القائمة.\n\n")
                + await RenderProductDetailAsync(session, ct);
        }

        var variantId = variantIdText.Length == 0 ? (Guid?)null : Guid.Parse(variantIdText);
        session.SetSelectedVariant(variantId);
        session.SetMenu(WhatsAppMenuState.VariantSelect);
        return await RenderVariantScreenAsync(session, ct);
    }

    private async Task<string> RenderVariantScreenAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        var productId = session.SelectedProductId!.Value;
        var configResult = await sender.Send(new GetStorefrontProductConfigurationsQuery([productId]), ct);
        var productResult = await sender.Send(new GetProductByIdQuery(productId), ct);

        if (!productResult.IsSuccess)
        {
            return RenderError(session);
        }

        var product = productResult.Value;
        var config = configResult.IsSuccess ? configResult.Value.FirstOrDefault() : null;
        var variant = session.SelectedVariantId is Guid variantId
            ? config?.Variants.FirstOrDefault(v => v.Id == variantId)
            : null;

        var price = variant?.Price ?? config?.BasePrice ?? product.Price;
        var available = variant is null
            ? product.AvailableStock > 0
            : !variant.IsOutOfStock;

        var label = variant?.Sku ?? L(session, "Standard", "قياسي");
        var availabilityText = available
            ? L(session, "In stock", "متوفر")
            : L(session, "Out of stock", "غير متوفر");

        var header = $"{label}\n" +
            L(session, $"Price: ${price.ToString("0.00", CultureInfo.InvariantCulture)}\n", $"السعر: ${price.ToString("0.00", CultureInfo.InvariantCulture)}\n") +
            L(session, $"Availability: {availabilityText}\n\n", $"التوفر: {availabilityText}\n\n");

        var actions = available
            ? L(session, "1. Buy Now\n2. Add to Cart\n3. View Product\n0. Back",
                "1. اشترِ الآن\n2. أضف إلى السلة\n3. عرض المنتج\n0. رجوع")
            : L(session, "3. View Product\n0. Back", "3. عرض المنتج\n0. رجوع");

        return header + actions;
    }

    private async Task<string> HandleVariantSelectAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            session.SetMenu(WhatsAppMenuState.ProductDetail);
            return await RenderProductDetailAsync(session, ct);
        }

        var applicationBaseUrl = (await platformSettings.GetEmailAsync(ct)).ApplicationBaseUrl;
        var productId = session.SelectedProductId!.Value;

        if (input == "3")
        {
            return L(session, "View it here: ", "شاهده هنا: ") + WhatsAppDeepLinkBuilder.ProductUrl(applicationBaseUrl, productId)
                + "\n\n" + RenderMain(session);
        }

        if (input != "1" && input != "2")
        {
            return L(session, "Please pick a valid option.\n\n", "يرجى اختيار خيار صحيح.\n\n") + await RenderVariantScreenAsync(session, ct);
        }

        var guestKey = session.IsLinked ? null : GuestKey(session);
        var addResult = await sender.Send(
            new AddCartItemCommand(productId, 1, guestKey, session.SelectedVariantId), ct);

        if (!addResult.IsSuccess)
        {
            return L(session, "Sorry, that couldn't be added to your cart right now.\n\n", "عذرًا، تعذّرت إضافة هذا إلى السلة الآن.\n\n")
                + RenderMain(session);
        }

        if (input == "1")
        {
            return L(session, "Added to cart! Complete your purchase here: ", "أُضيف إلى السلة! أكمل الشراء من هنا: ")
                + WhatsAppDeepLinkBuilder.CheckoutUrl(applicationBaseUrl) + "\n\n" + RenderMain(session);
        }

        return L(session, "Added to cart. View your cart here: ", "أُضيف إلى السلة. شاهد سلتك هنا: ")
            + WhatsAppDeepLinkBuilder.CartUrl(applicationBaseUrl) + "\n\n" + RenderMain(session);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    private string EnterSearchPrompt(WhatsAppConversationSession session)
    {
        session.SetMenu(WhatsAppMenuState.SearchPrompt);
        return L(session, "What game are you looking for? Reply with a name (or 0 for Main Menu).",
            "عن أي لعبة تبحث؟ أرسل الاسم (أو 0 للقائمة الرئيسية).");
    }

    private async Task<string> HandleSearchPromptAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0" || input.Length == 0)
        {
            return input == "0" ? RenderMain(session) : EnterSearchPrompt(session);
        }

        var term = input.Length > MaxSearchTermLength ? input[..MaxSearchTermLength] : input;
        session.SetMenu(WhatsAppMenuState.SearchResults);
        SetContext(session, new MenuContext(Term: term, Page: 1));
        return await RenderProductsAsync(session, ct, term);
    }

    private async Task<string> HandleSearchResultsAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        var context = GetContext(session);

        if (input == "0")
        {
            return RenderMain(session);
        }

        if (input == "9" || input == "8")
        {
            var delta = input == "9" ? 1 : -1;
            SetContext(session, context with { Page = Math.Max(1, context.Page + delta) });
            return await RenderProductsAsync(session, ct, context.Term);
        }

        if (!TryResolveListGuid(context, input, out var productId) || productId is null)
        {
            return L(session, "Please pick a number from the list.\n\n", "يرجى اختيار رقم من القائمة.\n\n")
                + await RenderProductsAsync(session, ct, context.Term);
        }

        session.SetSelectedProduct(productId);
        session.SetMenu(WhatsAppMenuState.ProductDetail);
        return await RenderProductDetailAsync(session, ct);
    }

    // ── Cart ─────────────────────────────────────────────────────────────────

    private async Task<string> EnterCartAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        session.SetMenu(WhatsAppMenuState.Cart);
        return await RenderCartAsync(session, ct);
    }

    private async Task<string> RenderCartAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        var guestKey = session.IsLinked ? null : GuestKey(session);
        var result = await sender.Send(new GetCartQuery(guestKey), ct);
        if (!result.IsSuccess)
        {
            return RenderError(session);
        }

        var cart = result.Value;
        if (cart.Items.Count == 0)
        {
            return L(session, "Your cart is empty.\n\n0. Main Menu", "سلتك فارغة.\n\n0. القائمة الرئيسية");
        }

        var lines = cart.Items.Select(i =>
            $"• {i.ProductNameEn} x{i.Quantity} — ${i.LineTotal.ToString("0.00", CultureInfo.InvariantCulture)}");
        var total = L(session, $"\nTotal: ${cart.Totals.Total.ToString("0.00", CultureInfo.InvariantCulture)}\n\n",
            $"\nالإجمالي: ${cart.Totals.Total.ToString("0.00", CultureInfo.InvariantCulture)}\n\n");

        var applicationBaseUrl = (await platformSettings.GetEmailAsync(ct)).ApplicationBaseUrl;
        var footer = L(session, $"Checkout: {WhatsAppDeepLinkBuilder.CheckoutUrl(applicationBaseUrl)}\n\n0. Main Menu",
            $"إتمام الشراء: {WhatsAppDeepLinkBuilder.CheckoutUrl(applicationBaseUrl)}\n\n0. القائمة الرئيسية");

        var header = L(session, "Your Cart:\n", "سلتك:\n");
        return header + string.Join('\n', lines) + total + footer;
    }

    private async Task<string> HandleCartAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            return RenderMain(session);
        }

        return await RenderCartAsync(session, ct);
    }

    // ── Orders ───────────────────────────────────────────────────────────────

    private async Task<string> RenderOrdersAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        session.SetMenu(WhatsAppMenuState.Orders);
        var result = await sender.Send(new GetOrdersQuery(1, 5), ct);
        if (!result.IsSuccess)
        {
            return RenderError(session);
        }

        var orders = result.Value.Items;
        if (orders.Count == 0)
        {
            return L(session, "You have no orders yet.\n\n0. Main Menu", "لا توجد لديك طلبات بعد.\n\n0. القائمة الرئيسية");
        }

        var lines = orders.Select(o =>
            $"• {o.OrderNumber} — {o.Status} — ${o.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)} ({o.CreatedOnUtc:yyyy-MM-dd})");

        var header = L(session, "Your recent orders:\n", "طلباتك الأخيرة:\n");
        var footer = L(session, "\n0. Main Menu", "\n0. القائمة الرئيسية");
        return header + string.Join('\n', lines) + footer;
    }

    private Task<string> HandleOrdersAsync(WhatsAppConversationSession session, string input, CancellationToken ct) =>
        input == "0" ? Task.FromResult(RenderMain(session)) : RenderOrdersAsync(session, ct);

    // ── Alerts ───────────────────────────────────────────────────────────────

    private async Task<string> RenderAlertsAsync(WhatsAppConversationSession session, CancellationToken ct)
    {
        session.SetMenu(WhatsAppMenuState.Alerts);
        var result = await sender.Send(new GetMyAlertSubscriptionsQuery(), ct);
        if (!result.IsSuccess)
        {
            return RenderError(session);
        }

        var alerts = result.Value;
        if (alerts.Count == 0)
        {
            return L(session, "You have no alert subscriptions.\n\n0. Main Menu", "لا توجد لديك تنبيهات مفعّلة.\n\n0. القائمة الرئيسية");
        }

        SetContext(session, new MenuContext(Ids: alerts.Select(a => a.Id.ToString()).ToList()));

        var lines = alerts.Select((a, i) => $"{i + 1}. {a.ProductNameEn} — {a.AlertType}");
        var header = L(session, "Your alerts (reply with a number to remove it):\n", "تنبيهاتك (أرسل رقمًا لإزالته):\n");
        var footer = L(session, "\n0. Main Menu", "\n0. القائمة الرئيسية");
        return header + string.Join('\n', lines) + footer;
    }

    private async Task<string> HandleAlertsAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "0")
        {
            return RenderMain(session);
        }

        var context = GetContext(session);
        if (TryResolveListGuid(context, input, out var alertIdText) && alertIdText is not null)
        {
            var removeResult = await sender.Send(new RemoveAlertSubscriptionCommand(alertIdText.Value, null), ct);
            var prefix = removeResult.IsSuccess
                ? L(session, "Alert removed.\n\n", "تمت إزالة التنبيه.\n\n")
                : L(session, "Couldn't remove that alert.\n\n", "تعذّرت إزالة هذا التنبيه.\n\n");
            return prefix + await RenderAlertsAsync(session, ct);
        }

        return await RenderAlertsAsync(session, ct);
    }

    // ── Support ──────────────────────────────────────────────────────────────

    private string RenderSupportMenu(WhatsAppConversationSession session)
    {
        session.SetMenu(WhatsAppMenuState.SupportMenu);
        return L(session, "Support:\n1. New support request\n0. Main Menu",
            "الدعم:\n1. طلب دعم جديد\n0. القائمة الرئيسية");
    }

    private Task<string> HandleSupportMenuAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input == "1")
        {
            session.SetMenu(WhatsAppMenuState.SupportCompose);
            return Task.FromResult(L(session, "Please describe your issue in one message.",
                "يرجى وصف مشكلتك في رسالة واحدة."));
        }

        return Task.FromResult(input == "0" ? RenderMain(session) : RenderSupportMenu(session));
    }

    private async Task<string> HandleSupportComposeAsync(WhatsAppConversationSession session, string input, CancellationToken ct)
    {
        if (input.Length == 0)
        {
            return L(session, "Please describe your issue in one message (or 0 to cancel).",
                "يرجى وصف مشكلتك في رسالة واحدة (أو 0 للإلغاء).");
        }

        if (input == "0")
        {
            return RenderMain(session);
        }

        var subject = input.Length > 60 ? input[..60] : input;
        var customerUserId = session.CustomerUserId!;
        var result = await sender.Send(
            new CreateTicketCommand(customerUserId, customerUserId, subject, input, null, null, null, null, null, "WhatsApp", "WhatsApp", null),
            ct);

        if (!result.IsSuccess)
        {
            return L(session, "Sorry, we couldn't create your support request. Please try again later.\n\n", "عذرًا، تعذّر إنشاء طلب الدعم. حاول لاحقًا.\n\n")
                + RenderMain(session);
        }

        return L(session, $"Support request {result.Value.TicketNumber} created. We'll follow up by email.\n\n",
            $"تم إنشاء طلب الدعم {result.Value.TicketNumber}. سنتابع معك عبر البريد الإلكتروني.\n\n") + RenderMain(session);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static string GuestKey(WhatsAppConversationSession session) => $"whatsapp:{session.PhoneNumber}";

    private static string RenderError(WhatsAppConversationSession session) =>
        L(session, "Something went wrong. Please try again.\n\n0. Main Menu",
            "حدث خطأ ما. حاول مرة أخرى.\n\n0. القائمة الرئيسية");

    private static string L(WhatsAppConversationSession session, string en, string ar) =>
        session.LanguageCode == "ar" ? ar : en;

    /// <summary>Resolves a typed number back to the id shown at that position — for lists where every
    /// entry is a real entity id (categories, products, alerts). Never returns the empty sentinel; use
    /// <see cref="TryResolveListIndex"/> directly where that sentinel is meaningful (variant options).</summary>
    private static bool TryResolveListGuid(MenuContext context, string input, out Guid? id)
    {
        id = null;
        if (!TryResolveListIndex(context, input, out var raw) || raw is null || raw.Length == 0)
        {
            return false;
        }

        // TryParse, not Parse: raw comes from ContextJson, which this instance always writes as valid
        // GUIDs — but a corrupted/stale row (manual edit, a future format change, an old app version's
        // context surviving a deploy) must fail the selection gracefully, not throw FormatException and
        // abort the whole SaveChangesAsync with no reply ever sent to the user.
        if (!Guid.TryParse(raw, out var parsed))
        {
            return false;
        }

        id = parsed;
        return true;
    }

    private static bool TryResolveListIndex(MenuContext context, string input, out string? raw)
    {
        raw = null;
        if (context.Ids is null || !int.TryParse(input, out var index) || index < 1 || index > context.Ids.Count)
        {
            return false;
        }

        raw = context.Ids[index - 1];
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static MenuContext GetContext(WhatsAppConversationSession session)
    {
        if (string.IsNullOrWhiteSpace(session.ContextJson))
        {
            return new MenuContext();
        }

        try
        {
            return JsonSerializer.Deserialize<MenuContext>(session.ContextJson, JsonOptions) ?? new MenuContext();
        }
        catch (JsonException)
        {
            return new MenuContext();
        }
    }

    private static void SetContext(WhatsAppConversationSession session, MenuContext context) =>
        session.SetContext(JsonSerializer.Serialize(context, JsonOptions));

    /// <summary>Small per-state scratch data: the ids currently numbered 1..N on screen (so a typed
    /// number maps to the right entity without re-querying with any risk of a different order), the
    /// active search term/page, and — while linking is in progress — which state to resume afterward.</summary>
    private sealed record MenuContext(List<string>? Ids = null, string? Term = null, int Page = 1, string? ReturnState = null);
}
