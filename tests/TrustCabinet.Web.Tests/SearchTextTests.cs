using TrustCabinet.Domain;

namespace TrustCabinet.Web.Tests;

/// <summary>Every search box matches without Vietnamese marks or case (Khánh, 19/11/2026: "search được không dấu").</summary>
public sealed class SearchTextTests
{
    [Theory]
    [InlineData("Họp thường kỳ", "hop thuong ky")]
    [InlineData("Nguyễn Văn Đông", "nguyen van dong")]
    [InlineData("ĐỖ ĐỨC ĐẠT", "do duc dat")]
    [InlineData("Thư ký", "thu ky")]
    [InlineData("Báo cáo 100%_xong", "bao cao 100%_xong")]
    public void Folds_marks_case_and_d_with_stroke(string text, string folded)
        => Assert.Equal(folded, SearchText.Fold(text));

    [Fact]
    public void A_decomposed_text_folds_like_a_composed_one()
        => Assert.Equal(SearchText.Fold("Lộc"), SearchText.Fold("Lộc".Normalize(System.Text.NormalizationForm.FormD)));

    [Fact]
    public void Contains_matches_a_folded_needle_and_never_a_missing_text()
    {
        Assert.True(SearchText.Contains("Phạm Chủ Toạ", SearchText.Fold("chu toa")));
        Assert.False(SearchText.Contains(null, "a"));
        Assert.False(SearchText.Contains("Lê", SearchText.Fold("lo")));
    }
}
