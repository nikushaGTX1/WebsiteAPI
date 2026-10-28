using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public sealed class ClientIpResolverTests
{
    private static HttpContext Context(string? key, params (string Name, string Value)[] headers)
    {
        var settings = new Dictionary<string, string?>();
        if (key is not null) settings["Proxy:SharedKey"] = key;
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context;
    }

    [Fact]
    public void WithoutKeyKeepsLegacyFirstForwardedIp()
    {
        Assert.Equal("1.1.1.1", ClientIpResolver.Resolve(Context(null, ("X-Forwarded-For", "1.1.1.1, 2.2.2.2"))));
    }

    [Fact]
    public void SpoofedForwardedForIsIgnoredWhenKeyIsSet()
    {
        var context = Context("secret", ("X-Forwarded-For", "9.9.9.9, 3.3.3.3"));
        Assert.Equal("3.3.3.3", ClientIpResolver.Resolve(context));
    }

    [Fact]
    public void ProxyWithKeyPassesVisitorIp()
    {
        var context = Context(
            "secret",
            ("X-Velven-Proxy-Key", "secret"),
            ("X-Velven-Client-Ip", "4.4.4.4"),
            ("X-Forwarded-For", "5.5.5.5"));
        Assert.Equal("4.4.4.4", ClientIpResolver.Resolve(context));
    }

    [Fact]
    public void WrongProxyKeyIsNotTrusted()
    {
        var context = Context(
            "secret",
            ("X-Velven-Proxy-Key", "guess"),
            ("X-Velven-Client-Ip", "4.4.4.4"),
            ("X-Forwarded-For", "6.6.6.6"));
        Assert.Equal("6.6.6.6", ClientIpResolver.Resolve(context));
    }
}
