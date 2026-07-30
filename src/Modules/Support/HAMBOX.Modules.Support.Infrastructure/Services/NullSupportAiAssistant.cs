using HAMBOX.Modules.Support.Application.Abstractions;

namespace HAMBOX.Modules.Support.Infrastructure.Services;

/// <summary>No-op default for <see cref="ISupportAiAssistant"/> — see the interface's doc
/// comment. Replace the DI registration in <c>SupportInfrastructureExtensions</c> with a real
/// implementation when AI support is actually built.</summary>
internal sealed class NullSupportAiAssistant : ISupportAiAssistant
{
    public Task<string?> SummarizeTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> SuggestReplyAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> AnalyzeSentimentAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<Guid?> SuggestCategoryAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);

    public Task<Guid?> SuggestPriorityAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);

    public Task<IReadOnlyList<Guid>> SuggestKnowledgeArticlesAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);
}
