namespace Api.Cookies;

public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";

    public const string AuthPath = "/api/v1/auth";

    public static void Set(HttpResponse response, string plainToken, DateTime expiresAtUtc, bool secure)
    {
        response.Cookies.Append(
            Name,
            plainToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = AuthPath,
                Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero)
            });
    }

    public static void Delete(HttpResponse response, bool secure)
    {
        response.Cookies.Delete(
            Name,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = AuthPath
            });
    }

    public static bool TryGet(HttpRequest request, out string plainToken)
    {
        return request.Cookies.TryGetValue(Name, out plainToken!);
    }
}
