using System.Globalization;
using System.Text.RegularExpressions;

namespace FlatPlate.App.Services;

/// <summary>Persistence requires an explicit zone; the parser's wall-time preview remains unchanged.</summary>
internal static class PriceCsvImportTimestamp
{
    private static readonly Regex Pattern = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant);
    private static readonly string[] WallFormats = { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" };
    private static readonly string[] OffsetFormats = { "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz" };

    public static bool TryUtc(string text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(text) || !Pattern.IsMatch(text)) return false;
        if (text.EndsWith('Z'))
        {
            if (!DateTime.TryParseExact(text[..^1], WallFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)) return false;
            utc = DateTime.SpecifyKind(wall, DateTimeKind.Utc);
            return true;
        }
        if (!DateTimeOffset.TryParseExact(text, OffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset)) return false;
        utc = offset.UtcDateTime;
        return true;
    }
}
