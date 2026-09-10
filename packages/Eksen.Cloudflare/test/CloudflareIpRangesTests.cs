using System.Net;
using Eksen.Cloudflare;

namespace Eksen.Cloudflare.Tests;

public sealed class CloudflareIpRangesTests
{
    [Theory]
    [InlineData("172.70.94.93", true)]
    [InlineData("162.158.152.165", true)]
    [InlineData("104.16.0.1", true)]
    [InlineData("66.249.72.237", false)]
    [InlineData("40.77.167.85", false)]
    [InlineData("10.0.1.2", false)]
    public void Contains_Should_Match_Only_Cloudflare_Networks(string address, bool expected)
    {
        var ranges = CloudflareIpRanges.Parse(["162.158.0.0/15", "104.16.0.0/13", "172.64.0.0/13"]);

        Assert.Equal(expected, ranges.Contains(IPAddress.Parse(address)));
    }

    // Kestrel and IIS routinely hand over IPv4-mapped IPv6 peers, which must still match an IPv4 range.
    [Fact]
    public void Contains_Should_Match_IPv4_Mapped_IPv6_Peers()
    {
        var ranges = CloudflareIpRanges.Parse(["172.64.0.0/13"]);
        var mapped = IPAddress.Parse("172.70.94.93").MapToIPv6();

        Assert.True(mapped.IsIPv4MappedToIPv6);
        Assert.True(ranges.Contains(mapped));
    }

    [Fact]
    public void Contains_Should_Match_IPv6_Networks()
    {
        var ranges = CloudflareIpRanges.Parse(["2400:cb00::/32"]);

        Assert.True(ranges.Contains(IPAddress.Parse("2400:cb00::1")));
        Assert.False(ranges.Contains(IPAddress.Parse("2a02:ff0::1")));
    }

    [Fact]
    public void Parse_Should_Ignore_Blank_And_Comment_And_Malformed_Lines()
    {
        var ranges = CloudflareIpRanges.Parse(["", "  ", "# comment", "not-a-cidr", "172.64.0.0/13"]);

        Assert.Single(ranges.Networks);
    }

    [Fact]
    public void Empty_Should_Never_Contain_Anything()
    {
        Assert.False(CloudflareIpRanges.Empty.Contains(IPAddress.Parse("172.70.94.93")));
        Assert.True(CloudflareIpRanges.Empty.IsEmpty);
    }

    [Fact]
    public void Contains_Should_Return_False_For_Null_Address()
    {
        var ranges = CloudflareIpRanges.Parse(["172.64.0.0/13"]);

        Assert.False(ranges.Contains(null));
    }
}
