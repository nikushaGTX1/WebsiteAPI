using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class PartnerWatermarkTests
{
    private static readonly ImageAiOptions Options = new();

    [Theory]
    [InlineData("third_party", "myhome.ge", "partner_brand")]       // answer cached before the partner was added
    [InlineData("own_brand", "MYHOME .GE", "partner_brand")]
    [InlineData("own_brand", "myhome.ge | velven", "partner_brand")]
    [InlineData("own_brand", "velven", "own_brand")]                 // Velven's own mark stays own
    [InlineData("third_party", "ss.ge", "third_party")]              // other companies are never removable
    [InlineData("third_party", "myhome.ge | ss.ge", "third_party")]  // mixed with another company
    [InlineData("third_party", null, "third_party")]                 // unreadable logo
    [InlineData("none", null, "none")]
    public void OnlyPartnerAndOwnMarksBecomeRemovable(string classification, string? text, string expected) =>
        Assert.Equal(expected, Options.PublicClassification(classification, text));

    [Fact]
    public void TheAiServiceTreatsPartnerMarksLikeOurOwn() =>
        Assert.Equal(["velven", "myhome"], Options.RemovableBrandTerms);
}
