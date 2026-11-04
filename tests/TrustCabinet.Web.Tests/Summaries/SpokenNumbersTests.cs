using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-36 — numbers read aloud in a transcript are written as digits before the model sees them.</summary>
public sealed class SpokenNumbersTests
{
    [Theory]
    [InlineData("mười tám trên hai mươi nhiệm vụ", "18 trên 20 nhiệm vụ")]
    [InlineData("đạt chín mươi phần trăm kế hoạch", "đạt 90% kế hoạch")]
    [InlineData("một phẩy hai tỷ đồng", "1,2 tỷ đồng")]
    [InlineData("bốn mươi lăm triệu đồng", "45 triệu đồng")]
    [InlineData("quý một năm hai không hai bảy", "quý 1 năm 2027")]
    [InlineData("một trăm lẻ năm người", "105 người")]
    [InlineData("hai máy quét", "2 máy quét")]
    [InlineData("ba năm hai tháng", "ba năm 2 tháng")]
    [InlineData("năm mươi người", "50 người")]
    [InlineData("từ tháng tư", "từ tháng 4")]
    public void Numbers_read_aloud_become_digits(string spoken, string written)
        => Assert.Equal(written, SpokenNumbers.Normalise(spoken));

    [Theory]
    [InlineData("một số ý kiến")]
    [InlineData("năm nay")]
    [InlineData("thứ tư")]
    [InlineData("Kính thưa các đồng chí")]
    public void Words_that_are_not_surely_a_number_stay(string text)
        => Assert.Equal(text, SpokenNumbers.Normalise(text));

    [Fact]
    public void Punctuation_around_a_number_is_kept()
        => Assert.Equal("trước ngày 15 tháng 11.", SpokenNumbers.Normalise("trước ngày mười lăm tháng mười một."));
}
