namespace HAMBOX.Modules.Messaging.Application.Abstractions;

/// <summary>
/// Lets the bot engine make downstream MediatR handlers see a linked WhatsApp session's customer as
/// "the current user" — the same <c>ICurrentUserService</c> every Commerce/Catalog handler already
/// reads from, just populated from a verified WhatsApp link instead of a JWT. Kept as an Application-layer
/// abstraction (implemented in Infrastructure against <c>IHttpContextAccessor</c>) so the engine stays
/// framework-agnostic, matching every other module's Domain/Application/Infrastructure split.
/// </summary>
public interface IWhatsAppUserContextScope
{
    /// <summary>Makes <c>ICurrentUserService</c> resolve to <paramref name="customerUserId"/> for the
    /// lifetime of the returned scope. Must be disposed before the webhook request completes.</summary>
    IDisposable ActAsCustomer(string customerUserId);
}
