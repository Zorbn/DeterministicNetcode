using System;
using System.Net;
using System.Text;

namespace DeterministicNetcode.Net;

public class NetHost : INetPeer
{
    // Peers that take more than this long to start when told to do so
    // will be disconnected from the game.
    // TODO: Notify peers about disconnections.
    // TODO: Or go back to the lobby/main menu and the players have to restart if somebody disconnects.
    private const float StartingGameTime = 1.0f;

    public bool IsHost => true;
    public NetState State => _state;
    public NetMessenger Messenger { get; } = new();

    private NetState _state = NetState.InLobby;

    private bool[] _havePeersStarted;
    // TODO: The host and peers should end the game if they stop receiving messages.
    // TODO: private float _startingGameTimer;

    // Used to store the result of Receive.
    private IPEndPoint _cachedEndPoint = new(IPAddress.Any, 0);

    public void BeginStartingGame()
    {
        if (_state != NetState.InLobby) return;
        _state = NetState.StartingGame;
        _havePeersStarted = new bool[Messenger.PeerCount];
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
        while (true)
        {
            var receivedSpan = Messenger.Receive(ref _cachedEndPoint);
            if (receivedSpan.Length == 0) break;

            var packetType = (PacketType)receivedSpan[0];

            switch (packetType)
            {
                case PacketType.Hello:
                    if (Messenger.PeerCount >= NetMessenger.MaxPeers)
                    {
                        Console.WriteLine("Failed to accept new peer, max peers already reached!");
                        break;
                    }

                    // If the peer doesn't receive acknowledgement they will keep saying hello,
                    // until an acknowledgement is successfully received.
                    Messenger.SendAcknowledgement(PacketType.Hello, _cachedEndPoint);

                    if (!Messenger.HasPeer(_cachedEndPoint)) Messenger.AddPeer(_cachedEndPoint);

                    break;
                default:
                    Console.WriteLine($"Got invalid packet, #{receivedSpan[0]}!");
                    break;
            }
        }
    }

    private void PollStartingGame()
    {
        var isWaitingOnPeer = false;

        for (var i = 0; i < _havePeersStarted.Length; i++)
        {
            if (_havePeersStarted[i]) continue;

            isWaitingOnPeer = true;
            SendAddPeers(i);
        }

        if (!isWaitingOnPeer)
        {
            _state = NetState.InGame;
            return;
        }

        while (true)
        {
            var receivedSpan = Messenger.Receive(ref _cachedEndPoint);
            if (receivedSpan.Length == 0) break;
            if (!NetMessenger.IsAcknowledgement(PacketType.AddPeers, receivedSpan)) continue;

            for (var i = 0; i < Messenger.PeerCount; i++)
            {
                if (!Messenger.Peers[i].Equals(_cachedEndPoint)) continue;

                _havePeersStarted[i] = true;
                break;
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
                case PacketType.InputState:
                    Messenger.HandleInputState(_cachedEndPoint, stepIndex);
                    break;
            }
        }
    }

    private void SendAddPeers(int toIndex)
    {
        Messenger.Buffer[0] = (byte)PacketType.AddPeers;
        Messenger.Buffer[1] = (byte)Messenger.PeerCount;
        // Make sure not to tell the peer to add a copy of it's own end point.
        if (Messenger.Buffer[1] > 0) Messenger.Buffer[1]--;

        var currentOffset = 2;

        for (var i = 0; i < Messenger.PeerCount; i++)
        {
            if (i == toIndex) continue;
            var peer = Messenger.Peers[i];

            var bytesWritten = WritePeerToBuffer(peer, currentOffset);

            if (bytesWritten == 0)
            {
                // Failed to write the peer to the buffer.
                return;
            }

            currentOffset += bytesWritten;
        }

        Messenger.SendFromBuffer(currentOffset, Messenger.Peers[toIndex]);
    }

    private int WritePeerToBuffer(IPEndPoint peer, int offset)
    {
        var ipEndPointString = peer.ToString();

        Messenger.Buffer[offset] = (byte)ipEndPointString.Length;
        offset++;

        var destinationSpan = new Span<byte>(Messenger.Buffer, offset, Messenger.Buffer.Length - offset);
        Encoding.ASCII.GetBytes(ipEndPointString, destinationSpan);

        return ipEndPointString.Length + 1;
    }

    public void Dispose()
    {
        Messenger.Dispose();
    }
}