namespace Shared.Infrastructure.Common;

/// <summary>Throw when a requested resource doesn't exist — mapped to 404 NOT_FOUND.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Throw for a business-rule conflict (e.g. duplicate email) — mapped to 409 CONFLICT.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Throw when credentials are missing/invalid (e.g. wrong password at login) — mapped to
/// 401 UNAUTHORIZED. Distinct from UnauthorizedAccessException, which is for an authenticated
/// caller lacking permission (403 FORBIDDEN, e.g. a Student hitting an Admin-only endpoint).</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
