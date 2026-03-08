namespace TraditionalEats.IdentityService.Exceptions;

/// <summary>
/// Thrown when Apple Sign In token has no email and no existing user link. Client should prompt for email and retry.
/// </summary>
public class AppleEmailRequiredException : Exception
{
    public AppleEmailRequiredException(string message) : base(message) { }
}
