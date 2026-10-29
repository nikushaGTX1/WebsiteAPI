using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class ListingTextNormalizerTests
{
    [Theory]
    [InlineData("შარაშიძე ქ. ქ., 30, დიღომი 1-9", "შარაშიძე ქ., 30, დიღომი 1-9")]
    [InlineData("შარაშიძე ქ. ქ.", "შარაშიძე ქ.")]
    [InlineData("ქ. ქ. ქ.", "ქ.")]
    [InlineData("ვარაზი ა. ქ., ლისი", "ვარაზი ა. ქ., ლისი")]   // different abbreviations stay
    [InlineData("Rustaveli Ave. Ave.", "Rustaveli Ave.")]
    [InlineData("  Vake ,  Tbilisi ", "Vake, Tbilisi")]
    [InlineData("Ilia Chavchavadze Avenue, Vake", "Ilia Chavchavadze Avenue, Vake")]
    [InlineData(null, null)]
    public void CleanAddressRemovesRepeatedAbbreviations(string? input, string? expected) =>
        Assert.Equal(expected, ListingTextNormalizer.CleanAddress(input));

    [Theory]
    [InlineData("  3-room apartment ", "3-room apartment")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void BlankTitleTranslationMeansNone(string? input, string? expected) =>
        Assert.Equal(expected, ListingTextNormalizer.CleanTitle(input));
}
