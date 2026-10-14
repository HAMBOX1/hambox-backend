using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// An administrator's authenticator-app (TOTP, RFC 6238) enrolment. The shared secret is stored only in its
/// protected (encrypted) form. A credential starts unconfirmed — created when enrolment begins — and only
/// becomes a login factor once the admin proves their app generates valid codes (<see cref="Confirm"/>).
/// </summary>
public sealed class AdminTotpCredential : Entity
{
    private AdminTotpCredential()
    {
    }

    private AdminTotpCredential(Guid id, Guid userId, string protectedSecret)
        : base(id)
    {
        UserId = userId;
        ProtectedSecret = protectedSecret;
    }

    public Guid UserId { get; private set; }

    /// <summary>The base-32 secret, encrypted at rest (see <c>ICodeProtector</c>).</summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    public bool IsConfirmed { get; private set; }

    public DateTimeOffset? ConfirmedOnUtc { get; private set; }

    /// <summary>
    /// The highest RFC 6238 time step already accepted. A code is only valid for a step strictly greater than
    /// this, so a code that was observed or phished cannot be replayed inside its own 30-second window.
    /// </summary>
    public long LastUsedTimeStep { get; private set; }

    public static AdminTotpCredential Begin(Guid userId, string protectedSecret)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        return new AdminTotpCredential(Guid.NewGuid(), userId, protectedSecret);
    }

    /// <summary>Replaces the pending secret while enrolment has not been confirmed yet.</summary>
    public void ReplaceSecret(string protectedSecret)
    {
        if (IsConfirmed)
        {
            throw new InvalidOperationException("A confirmed authenticator cannot be re-enrolled without removing it first.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ProtectedSecret = protectedSecret;
        LastUsedTimeStep = 0;
    }

    public void Confirm(long acceptedTimeStep)
    {
        IsConfirmed = true;
        ConfirmedOnUtc = DateTimeOffset.UtcNow;
        LastUsedTimeStep = acceptedTimeStep;
    }

    public void RecordUse(long acceptedTimeStep)
    {
        if (acceptedTimeStep > LastUsedTimeStep)
        {
            LastUsedTimeStep = acceptedTimeStep;
        }
    }
}
