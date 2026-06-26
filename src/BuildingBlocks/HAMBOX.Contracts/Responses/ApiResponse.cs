using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Contracts.Responses;

/// <summary>
/// Represents a standard API response contract.
/// </summary>
/// <typeparam name="T">The response data type.</typeparam>
/// <param name="IsSuccess">A value indicating whether the request succeeded.</param>
/// <param name="Data">The response data.</param>
/// <param name="Error">The response error.</param>
public sealed record ApiResponse<T>(bool IsSuccess, T? Data, Error Error)
{
    /// <summary>
    /// Creates a successful API response.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <returns>A successful API response.</returns>
    public static ApiResponse<T> Success(T data) => new(true, data, Error.None);

    /// <summary>
    /// Creates a failed API response.
    /// </summary>
    /// <param name="error">The response error.</param>
    /// <returns>A failed API response.</returns>
    public static ApiResponse<T> Failure(Error error) => new(false, default, error);
}
