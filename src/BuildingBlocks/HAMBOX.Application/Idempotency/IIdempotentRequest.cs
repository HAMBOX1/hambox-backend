namespace HAMBOX.Application.Idempotency;

/// <summary>
/// Marks a MediatR command as idempotent: requests carrying the same client-supplied
/// <c>Idempotency-Key</c> header execute the underlying handler at most once, with
/// subsequent requests replaying the original <see cref="HAMBOX.SharedKernel.Results.Result{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The value type wrapped by the command's <c>Result&lt;TValue&gt;</c> response.</typeparam>
/// <remarks>
/// Opt in by adding this interface to a command declaration — no other wiring is required.
/// The IdempotencyBehavior pipeline behavior picks up any command implementing it.
/// </remarks>
public interface IIdempotentRequest<TValue>;
