namespace Api.Configuration;

public static class CorsOriginValidator
{
    public static bool IsValidOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/")
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return true;
    }
}
