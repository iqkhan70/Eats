namespace TraditionalEats.WebApp.Services;

public class SessionExpiredException : Exception
{
    public SessionExpiredException() : base("Your session has expired. Please login again.")
    {
    }

    public SessionExpiredException(string message) : base(message)
    {
    }
}
