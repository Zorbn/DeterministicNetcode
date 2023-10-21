using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // MaxPeers doesn't include the local peer.
    public const int MaxPeers = 3;

    public readonly int Port;
    public readonly byte[] Buffer = new byte[BufferSize];

    public readonly IPEndPoint[] Peers = new IPEndPoint[MaxPeers];
    public readonly InputState?[] PeerInputStates = new InputState?[MaxPeers];
    public int PeerCount { get; private set; }
    public readonly IPEndPoint LocalEndPoint;

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

    public void SendAcknowledgement(PacketType ofType, IPEndPoint to)
    {
        Buffer[0] = (byte)PacketType.Acknowledge;
        Buffer[1] = (byte)ofType;

        SendFromBuffer(2, to);
    }

    public static bool IsAcknowledgement(PacketType ofType, Span<byte> bytes)
    {
        if (bytes.Length < 2) return false;

        return (PacketType)bytes[0] == PacketType.Acknowledge && (PacketType)bytes[1] == ofType;
    }

    public void SendInputState(List<InputState> inputStates)
    {
        Debug.Assert(inputStates.Count <= InputState.SavedInputStateCount);

        var offset = 0;

        Buffer[offset] = (byte)PacketType.InputState;
        offset++;
        Buffer[offset] = (byte)inputStates.Count;
        offset++;

        foreach (var inputState in inputStates)
        {
            var inputStateLength = inputState.WriteBytes(Buffer, offset);
            offset += inputStateLength;
        }

        SendFromBuffer(offset);
    }

    public void ClearInputStates()
    {
        for (var i = 0; i < PeerCount; i++)
        {
            PeerInputStates[i] = null;
        }
    }

    public bool HasAllPeerInputStates()
    {
        for (var i = 0; i < PeerCount; i++)
        {
            if (PeerInputStates[i] is null) return false;
        }

        return true;
    }

    public void HandleInputState(EndPoint from, int forStepIndex)
    {
        var offset = 1;
        var inputStateCount = Buffer[offset];
        offset++;

        // Try to find an input from the current step we're preparing to simulate.
        InputState? inputState = null;
        for (var i = 0; i < inputStateCount; i++)
        {
            var receivedInputState = InputState.FromBytes(Buffer, ref offset);
            if (receivedInputState.StepIndex != forStepIndex) continue;

            inputState = receivedInputState;
            break;
        }

        if (inputState is null) return;

        for (var i = 0; i < PeerCount; i++)
        {
            if (!Peers[i].Equals(from)) continue;

            PeerInputStates[i] = inputState;
            break;
        }
    }

    public void SendFromBuffer(int length, IPEndPoint to = null)
    {
        var bytes = new Span<byte>(Buffer, 0, length);

        if (to is not null)
        {
            _udpClient.Client.SendTo(bytes, to);
            return;
        }

        for (var i = 0; i < PeerCount; i++)
        {
            _udpClient.Client.SendTo(bytes, Peers[i]);
        }
    }

    public Span<byte> Receive(ref IPEndPoint endPoint)
    {
        if (_udpClient.Available == 0) return new Span<byte>();

        var destinationSpan = new Span<byte>(Buffer, 0, BufferSize);
        var destinationEndPoint = (EndPoint)endPoint;
        // TODO: Handle the case where the peer has disconnected (currently throws).
        var receivedCount = _udpClient.Client.ReceiveFrom(destinationSpan, ref destinationEndPoint);
        endPoint = (IPEndPoint)destinationEndPoint;

        return new Span<byte>(Buffer, 0, receivedCount);
    }

    public void AddPeer(IPEndPoint peer)
    {
        if (PeerCount >= MaxPeers) throw new ArgumentException("Failed to add a new peer, all peer slots are full!");

        Peers[PeerCount] = peer;
        PeerCount++;
    }

    // Peers may be represented differently by different machines.
    // To be safe, only compare peers you get from the same source.
    public bool HasPeer(IPEndPoint peer)
    {
        for (var i = 0; i < PeerCount; i++)
        {
            if (Peers[i].Equals(peer))
            {
                return true;
            }
        }

        return false;
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