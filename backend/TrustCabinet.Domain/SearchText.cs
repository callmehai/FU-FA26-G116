using System.Globalization;
using System.Text;

namespace TrustCabinet.Domain;

/// <summary>
/// Every search box matches without Vietnamese marks or case: "hop thuong" finds "Họp thường", "nguyen" finds "Nguyễn",
/// "dong" finds "Đông". This is the in-memory side; a search in the database folds both sides with PostgreSQL's
/// <c>unaccent</c> and <c>lower</c>, whose rules map the same letters (đ and Đ to d included).
/// </summary>
public static class SearchText
{
    /// <summary>Lower case, combining marks removed, đ/Đ to d.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            folded.Append(c is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(c));
        }

        return folded.ToString();
    }

    /// <summary>Whether <paramref name="text"/> contains <paramref name="foldedNeedle"/>, already passed through <see cref="Fold"/>.</summary>
    public static bool Contains(string? text, string foldedNeedle)
        => text is not null && Fold(text).Contains(foldedNeedle, StringComparison.Ordinal);
}
