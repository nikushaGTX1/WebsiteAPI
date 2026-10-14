using System.ComponentModel.DataAnnotations;
using System.Net;
using Website_API.DTO;
using Website_API.Services;
using Xunit;
namespace Website_API.Tests;
public class MyHomeVerifierTests
{
    public static string Active(string id) => "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"dehydratedState\":{\"queries\":[{\"state\":{\"data\":{\"data\":{\"statement\":{\"statement_id\":" + id + ",\"area\":60}}}}}]}}}}</script>";
    [Fact] public void ExactHydratedStatementIsActive() => Assert.Equal("ACTIVE", MyHomeVerifier.Classify("12345678", 200, Active("12345678")).Status);
    [Fact] public void OtherListingAndGenericShellAreUncertain() {
        Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", 200, Active("87654321")).Status);
        Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", 200, "<h1>MyHome</h1>").Status);
        Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", 200, "<script id=__NEXT_DATA__>{invalid}</script>").Status);
    }
    [Theory] [InlineData(404)] [InlineData(410)]
    public void ConfirmedHttpRemoval(int status) => Assert.Equal("REMOVED", MyHomeVerifier.Classify("12345678", status, "Not found").Status);
    [Fact] public void VisibleDeletedMessageOnly() {
        const string phrase = "განცხადება რომელსაც ეძებდით წაშლილი ან არააქტიურია";
        Assert.Equal("REMOVED", MyHomeVerifier.Classify("12345678", 200, "<h1>" + phrase + "</h1>").Status);
        Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", 200, "<script>const translations='" + phrase + "';</script>").Status);
    }
    [Theory] [InlineData(403)] [InlineData(429)] [InlineData(401)]
    public void RestrictedAccessNeverRemoved(int status) => Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", status, "").Status);
    [Theory] [InlineData("Just a moment")] [InlineData("captcha")] [InlineData("Access denied")]
    public void ChallengesOverrideEven404(string html) => Assert.Equal("UNCERTAIN", MyHomeVerifier.Classify("12345678", 404, html).Status);
    [Theory] [InlineData("12345678", true)] [InlineData(" 12345678 ", true)] [InlineData("", true)] [InlineData(null, true)] [InlineData("abcd", false)] [InlineData("123", false)] [InlineData("01234567", false)] [InlineData("123456789", false)]
    public void CreateAndUpdateValidateOptionalId(string? id, bool expected) {
        foreach (var type in new[] { typeof(CreateApartmentDto), typeof(UpdateApartmentDto) }) {
            var property = type.GetProperty("MyHomeId")!;
            var validator = property.GetCustomAttributes(typeof(RegularExpressionAttribute), false).Cast<RegularExpressionAttribute>().Single();
            Assert.Equal(expected, validator.IsValid(id));
        }
    }
    [Fact] public async Task MissingAndInvalidIdsNeverRequestNetwork() {
        using var handler = new StubHandler((_, _) => throw new Exception("Must not request"));
        var verifier = new MyHomeVerifier(new HttpClient(handler));
        Assert.Equal("NO_MYHOME_ID", (await verifier.Verify(null, default)).Status);
        Assert.Equal("ERROR", (await verifier.Verify("123abc", default)).Status);
    }
    [Fact] public async Task TimeoutNeverRemoved() {
        var attempts = 0;
        using var handler = new StubHandler((_, _) => { attempts++; throw new TaskCanceledException("timeout"); });
        var result = await new MyHomeVerifier(new HttpClient(handler)).Verify("12345678", default);
        Assert.Equal("ERROR", result.Status); Assert.Equal(3, attempts);
    }
    [Fact] public async Task NetworkFailureNeverRemoved() {
        using var handler = new StubHandler((_, _) => throw new HttpRequestException("offline"));
        Assert.Equal("ERROR", (await new MyHomeVerifier(new HttpClient(handler)).Verify("12345678", default)).Status);
    }
    [Fact] public async Task TransientServerErrorRetriesThenSucceeds() {
        var attempts = 0;
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(++attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Active("12345678")) }));
        Assert.Equal("ACTIVE", (await new MyHomeVerifier(new HttpClient(handler)).Verify("12345678", default)).Status);
        Assert.Equal(2, attempts);
    }
    [Fact] public async Task ExternalRedirectIsNotFollowed() {
        using var handler = new StubHandler((_, _) => { var r = new HttpResponseMessage(HttpStatusCode.Found); r.Headers.Location = new Uri("https://example.com/login"); return Task.FromResult(r); });
        Assert.Equal("UNCERTAIN", (await new MyHomeVerifier(new HttpClient(handler)).Verify("12345678", default)).Status);
    }
}
public class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}
