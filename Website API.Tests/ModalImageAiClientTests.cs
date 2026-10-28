using Microsoft.Extensions.Options;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

// Live contract check against the deployed Modal app. Skips unless
// VELVEN_IMAGE_AI_ENDPOINT, VELVEN_IMAGE_AI_TOKEN and VELVEN_IMAGE_AI_SAMPLE are set.
public sealed class ModalImageAiClientTests
{
    [Fact]
    public async Task ProcessesOwnBrandSampleEndToEnd()
    {
        var endpoint = Environment.GetEnvironmentVariable("VELVEN_IMAGE_AI_ENDPOINT");
        var token = Environment.GetEnvironmentVariable("VELVEN_IMAGE_AI_TOKEN");
        var sample = Environment.GetEnvironmentVariable("VELVEN_IMAGE_AI_SAMPLE");
        if (endpoint is null || token is null || sample is null)
        {
            return;
        }

        var client = new ModalImageAiClient(
            new HttpClient(),
            Options.Create(new ImageAiOptions { Endpoint = endpoint, Token = token }));

        var result = await client.ProcessAsync(
            new ModalProcessRequest
            {
                ImageB64 = Convert.ToBase64String(await File.ReadAllBytesAsync(sample)),
                Mode = "process",
                AuthorizationBasis = "owned",
                OwnBrandTerms = ["velven"]
            },
            CancellationToken.None);

        Assert.Equal("own_brand", result.Classification);
        Assert.Equal("processed", result.Action);
        Assert.False(string.IsNullOrEmpty(result.ProcessedB64));
        Assert.Equal(64, result.ProcessedSha256?.Length);
        Assert.True(result.GpuSeconds > 0);
    }

    [Fact]
    public async Task RejectsWrongToken()
    {
        var endpoint = Environment.GetEnvironmentVariable("VELVEN_IMAGE_AI_ENDPOINT");
        if (endpoint is null)
        {
            return;
        }

        var client = new ModalImageAiClient(
            new HttpClient(),
            Options.Create(new ImageAiOptions { Endpoint = endpoint, Token = "wrong" }));

        var error = await Assert.ThrowsAsync<ModalImageAiException>(() => client.ProcessAsync(
            new ModalProcessRequest { ImageB64 = "aGk=" },
            CancellationToken.None));
        Assert.True(error.IsPermanent);
    }
}
