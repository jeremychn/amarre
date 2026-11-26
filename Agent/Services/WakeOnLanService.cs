using Remotely.Shared.Helpers;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

public interface IWakeOnLanService
{
    Task WakeDevice(string macAddress);
}

public class WakeOnLanService : IWakeOnLanService
{
    public async Task WakeDevice(string macAddress)
    {
        // 6 octets exigés : une adresse plus longue grossissait le paquet de diffusion.
        var macBytes = MacAddressHelper.GetBytes(macAddress);

        using var client = new UdpClient();

        var macData = Enumerable
            .Repeat(macBytes, 16)
            .SelectMany(x => x);

        var packet = Enumerable
            .Repeat((byte)0xFF, 6)
            .Concat(macData)
            .ToArray();

        var broadcastAddress = System.Net.IPAddress.Parse("255.255.255.255");
        // WOL usually uses port 9.
        var endpoint = new IPEndPoint(broadcastAddress, 9);
        await client.SendAsync(packet, packet.Length, endpoint);
    }
}
