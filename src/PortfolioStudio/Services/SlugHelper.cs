using System.Text;
using System.Text.RegularExpressions;

namespace PortfolioStudio.Services;

public static partial class SlugHelper
{
    public static string GenerateSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return $"item-{Guid.NewGuid():N}"[..13];

        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var slug = NonAlphanumericRegex().Replace(sb.ToString(), "-").Trim('-');
        slug = MultiHyphenRegex().Replace(slug, "-");

        return string.IsNullOrWhiteSpace(slug)
            ? $"item-{Guid.NewGuid():N}"[..13]
            : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiHyphenRegex();
}
