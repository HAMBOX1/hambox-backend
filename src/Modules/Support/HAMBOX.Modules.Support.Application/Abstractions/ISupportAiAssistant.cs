namespace HAMBOX.Modules.Support.Application.Abstractions;

/// <summary>
/// Extension point for AI-assisted support (§ AI preparation in the module spec). No real
/// implementation exists yet — <c>NullSupportAiAssistant</c> is the only registered
/// implementation and returns empty/no-op results. Wire a real provider here later without
/// touching any command handler that already calls this interface.
/// </summary>
public interface ISupportAiAssistant
{
    Task<string?> SummarizeTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<string?> SuggestReplyAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<string?> AnalyzeSentimentAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Guid?> SuggestCategoryAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Guid?> SuggestPriorityAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> SuggestKnowledgeArticlesAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
