using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Website_API.Services;
using Xunit;
namespace Website_API.Tests;
public class StorageUploadTests
{
    private static SupabaseStorageService Service(HttpMessageHandler handler) => new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Supabase:Url"] = "https://example.invalid", ["Supabase:SecretKey"] = "test", ["Supabase:Bucket"] = "apartments" }).Build(), NullLogger<SupabaseStorageService>.Instance);
    private static FormFile Photo() => new(new MemoryStream([1,2,3,4]), 0, 4, "photo", "photo.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
    [Fact] public async Task Transient502RetriesSamePathWithFreshBody() {
        var urls = new List<string>(); var upserts = new List<string>(); var bodies = new List<byte[]>();
        using var handler = new StubHandler(async (request, token) => {
            urls.Add(request.RequestUri!.ToString()); upserts.Add(request.Headers.GetValues("x-upsert").Single()); bodies.Add(await request.Content!.ReadAsByteArrayAsync(token));
            return new HttpResponseMessage(urls.Count == 1 ? HttpStatusCode.BadGateway : HttpStatusCode.OK) { Content = new StringContent("<html>temporary failure</html>") };
        });
        Assert.NotNull(await Service(handler).UploadImageAsync(Photo()));
        Assert.Equal(2, urls.Count); Assert.Single(urls.Distinct()); Assert.Equal(new[] { "false", "true" }, upserts);
        Assert.All(bodies, bytes => Assert.Equal(new byte[] {1,2,3,4}, bytes));
    }
    [Fact] public async Task PersistentFailureIsBoundedAndCleanupCannotMaskCause() {
        var attempts = 0;
        using var handler = new StubHandler((request, _) => {
            if (request.Method == HttpMethod.Delete) throw new HttpRequestException("cleanup unavailable");
            attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        });
        var error = await Assert.ThrowsAsync<StorageUploadException>(() => Service(handler).UploadImageAsync(Photo()));
        Assert.True(error.IsTransient); Assert.Equal(502, error.StatusCode); Assert.Equal(3, attempts);
    }
    [Fact] public async Task ForbiddenIsNotRetriedAndCancellationStopsRetries() {
        var attempts = 0;
        using var handler = new StubHandler((request, _) => {
            if (request.Method == HttpMethod.Post) attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        });
        var error = await Assert.ThrowsAsync<StorageUploadException>(() => Service(handler).UploadImageAsync(Photo()));
        Assert.False(error.IsTransient); Assert.Equal(1, attempts);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        using var canceled = new StubHandler((_, token) => { token.ThrowIfCancellationRequested(); throw new Exception(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(canceled).UploadImageAsync(Photo(), cancellationToken: cancel.Token));
    }
}
