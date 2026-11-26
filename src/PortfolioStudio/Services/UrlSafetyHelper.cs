namespace PortfolioStudio.Services;

public static class UrlSafetyHelper
{
    public static string? ToSafeExternalOrAnchorUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var trimmed = rawUrl.Trim();
        if (trimmed.StartsWith('#') || (trimmed.StartsWith('/') && !trimmed.StartsWith("//")))
            return trimmed;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return uri.AbsoluteUri;
        }

        return null;
    }

    public static bool IsValidSafeUrlOrEmpty(string? rawUrl)
    {
        return string.IsNullOrWhiteSpace(rawUrl) || ToSafeExternalOrAnchorUrl(rawUrl) != null;
    }
}
