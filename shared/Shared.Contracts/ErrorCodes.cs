namespace Shared.Contracts;

/// <summary>Standardized error codes returned in ApiResponse.Error.Code across every service.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string AiUnavailable = "AI_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
