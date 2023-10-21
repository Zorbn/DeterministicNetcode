using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DeterministicNetcode.Net;

public class NetMessenger : IDisposable
{
    public struct Address
    {
        public string Ip;
        public int Port;
    }

    public const int BufferSize = 1024;
    public const int MaxPeers = 4;

    public readonly int Port;
    public readonly byte[] Buffer = new byte[BufferSize];

    public readonly HashSet<EndPoint> Peers = new();
    public readonly EndPoint LocalEndPoint;

    private readonly UdpClient _udpClient;

    public NetMessenger()
    {
        var endPoint = new IPEndPoint(IPAddress.Any, 0);
        _udpClient = new UdpClient(endPoint);

        var localEndPoint = (IPEndPoint)_udpClient.Client.LocalEndPoint;
        if (localEndPoint is null)
        {
            throw new NullReferenceException("Socket has no end point!");
        }
        Port = localEndPoint.Port;
        LocalEndPoint = localEndPoint;
    }

    public void SendAcknowledgement(PacketType ofType, EndPoint to)
    {
        Buffer[0] = (byte)PacketType.Acknowledge;
        Buffer[1] = (byte)ofType;

        SendFromBuffer(2, to);
    }

    public bool IsAcknowledgement(PacketType ofType, Span<byte> bytes)
    {
        if (bytes.Length < 2) return false;

        return (PacketType)bytes[0] == PacketType.Acknowledge && (PacketType)bytes[1] == ofType;
    }

    public void SendFromBuffer(int length, EndPoint to = null)
    {
        var bytes = new Span<byte>(Buffer, 0, length);

        if (to is not null)
        {
            _udpClient.Client.SendTo(bytes, to);
            return;
        }

        foreach (var peer in Peers)
        {
            _udpClient.Client.SendTo(bytes, peer);
        }
    }

    public Span<byte> Receive(ref EndPoint endPoint)
    {
        if (_udpClient.Available == 0) return new Span<byte>();

        var destinationSpan = new Span<byte>(Buffer, 0, BufferSize);
        var receivedCount = _udpClient.Client.ReceiveFrom(destinationSpan, ref endPoint);
        return new Span<byte>(Buffer, 0, receivedCount);
    }

    public static IPEndPoint CreateIpEndPoint(string ip, int port)
    {
        var ipHostInfo = Dns.GetHostEntry(ip);
        var ipAddress = ipHostInfo.AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork);
        return new IPEndPoint(ipAddress, port);
    }

    public void Dispose()
    {
        _udpClient.Dispose();
    }
}