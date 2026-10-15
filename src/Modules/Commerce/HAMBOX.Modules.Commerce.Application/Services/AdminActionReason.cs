using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>Every manual order action carries a human reason (audit R43).</summary>
internal static class AdminActionReason
{
    public const int MinLength = 3;
    public const int MaxLength = 500;

    public static Result<string> Require(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinLength)
        {
            return Result.Failure<string>(CommerceErrors.ActionReasonRequired);
        }

        return Result.Success(trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed);
    }
}
