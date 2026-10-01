namespace Gabonet.Hubble.Tests;

using System.Net;
using Gabonet.Hubble.Security;
using Xunit;

public class IpAllowListTests
{
    [Theory]
    [InlineData("8.8.8.8", new string[0], true)]
    [InlineData("8.8.8.8", new[] { "*" }, true)]
    [InlineData("127.0.0.1", new[] { "127.0.0.1" }, true)]
    [InlineData("::1", new[] { "127.0.0.1" }, true)]
    [InlineData("::ffff:127.0.0.1", new[] { "127.0.0.1" }, true)]
    [InlineData("10.20.30.40", new[] { "10.0.0.0/8" }, true)]
    [InlineData("11.0.0.1", new[] { "10.0.0.0/8" }, false)]
    [InlineData("192.168.1.130", new[] { "192.168.1.128/25" }, true)]
    [InlineData("192.168.1.127", new[] { "192.168.1.128/25" }, false)]
    [InlineData("fd12::1", new[] { "fd00::/8" }, true)]
    [InlineData("fe80::1", new[] { "fd00::/8" }, false)]
    [InlineData("10.0.0.1", new[] { "fd00::/8" }, false)]
    [InlineData("8.8.8.8", new[] { "127.0.0.1", "not-an-ip", "10.0.0.0/99" }, false)]
    public void Evaluates_ips_and_cidr_ranges(string ip, string[] rules, bool expected)
    {
        Assert.Equal(expected, HubbleIpAllowList.IsAllowed(IPAddress.Parse(ip), rules));
    }

    [Fact]
    public void Unknown_client_ip_is_denied_when_rules_exist()
    {
        Assert.False(HubbleIpAllowList.IsAllowed(null, new[] { "127.0.0.1" }));
    }
}
