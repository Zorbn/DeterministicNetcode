using System;
using System.Net;
using System.Text;

namespace DeterministicNetcode.Net;

public class NetPeer : INetPeer
{
    public bool IsHost => false;
    public NetState State => _state;
    public NetMessenger Messenger { get; } = new();

    private readonly IPEndPoint _host;
    private NetState _state = NetState.InLobby;
    private bool _hasAddedPeers;

    // Used to store the result of Receive.
    private IPEndPoint _cachedEndPoint = new(IPAddress.Any, 0);

    public NetPeer(NetMessenger.Address hostAddress)
    {
        _host = NetMessenger.CreateIpEndPoint(hostAddress.Ip, hostAddress.Port);
        Messenger.AddPeer(_host);
    }

    public void Poll(int stepIndex)
    {
        switch (_state)
        {
            case NetState.InLobby:
                PollInLobby();
                break;
            case NetState.StartingGame:
                PollStartingGame();
                break;
            case NetState.InGame:
                PollInGame(stepIndex);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PollInLobby()
    {
        SendHelloToHost();

        while (true)
        {
            var receivedSpan = Messenger.Receive(ref _cachedEndPoint);
            if (receivedSpan.Length == 0) break;

            if (NetMessenger.IsAcknowledgement(PacketType.Hello, receivedSpan))
            {
                _state = NetState.StartingGame;
            }
        }
    }

    private void PollStartingGame()
    {
        while (true)
        {
            var receivedSpan = Messenger.Receive(ref _cachedEndPoint);
            if (receivedSpan.Length == 0) break;

            var packetType = (PacketType)receivedSpan[0];

            switch (packetType)
            {
                case PacketType.AddPeers:
                    HandleAddPeers(receivedSpan);
                    _state = NetState.InGame;
                    return;
            }
        }
    }

    private void PollInGame(int stepIndex)
    {
        while (true)
        {
            var receivedSpan = Messenger.Receive(ref _cachedEndPoint);
            if (receivedSpan.Length == 0) break;

            var packetType = (PacketType)receivedSpan[0];

            switch (packetType)
            {
                case PacketType.AddPeers:
                    HandleAddPeers(receivedSpan);
                    break;
                case PacketType.InputState:
                    Messenger.HandleInputState(_cachedEndPoint, stepIndex);
                    break;
            }
        }
    }

    private void HandleAddPeers(Span<byte> bytes)
    {
        if (_hasAddedPeers)
        {
            Messenger.SendAcknowledgement(PacketType.AddPeers, _host);
            return;
        }

        _hasAddedPeers = true;

        var receivedPeerCount = bytes[1];

        var currentOffset = 2;

        for (var i = 0; i < receivedPeerCount; i++)
        {
            var receivedPeer = ReadPeerFromBytes(bytes, ref currentOffset);
            Messenger.AddPeer(receivedPeer);
        }

        Messenger.SendAcknowledgement(PacketType.AddPeers, _host);
    }

    private IPEndPoint ReadPeerFromBytes(Span<byte> bytes, ref int currentOffset)
    {
        var ipEndPointStringLength = bytes[currentOffset];
        currentOffset++;
        var sourceSpan = new Span<byte>(Messenger.Buffer, currentOffset, ipEndPointStringLength);
        var ipEndPointString = Encoding.ASCII.GetString(sourceSpan);
        currentOffset += ipEndPointStringLength;

        return IPEndPoint.Parse(ipEndPointString);
    }

    private void SendHelloToHost()
    {
        Messenger.Buffer[0] = (byte)PacketType.Hello;
        Messenger.SendFromBuffer(1, _host);
    }

    public void Dispose()
    {
        Messenger.Dispose();
    }
}