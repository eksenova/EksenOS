using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace Eksen.Cloudflare;

public sealed class CloudflareIpRanges
{
    public static readonly CloudflareIpRanges Empty = new([]);

    public CloudflareIpRanges(IEnumerable<IPNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        Networks = [.. networks];
    }

    public ImmutableArray<IPNetwork> Networks { get; }

    public bool IsEmpty
    {
        get { return Networks.IsDefaultOrEmpty; }
    }

    public bool Contains(IPAddress? address)
    {
        if (address is null || IsEmpty)
        {
            return false;
        }

        var candidate = Normalize(address);

        foreach (var network in Networks)
        {
            if (network.BaseAddress.AddressFamily == candidate.AddressFamily
                && network.Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }

    public static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
        {
            return new IPAddress(address.GetAddressBytes());
        }

        return address;
    }

    public static CloudflareIpRanges Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var networks = new List<IPNetwork>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (IPNetwork.TryParse(trimmed, out var network))
            {
                networks.Add(network);
            }
        }

        return new CloudflareIpRanges(networks);
    }
}
