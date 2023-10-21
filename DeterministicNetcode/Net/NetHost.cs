using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeterministicNetcode.Net;

public class NetHost : INetPeer
{
    public int Port => _messenger.Port;

    private NetMessenger _messenger = new();
    private NetState _state = NetState.InLobby;

    // Used to store the result of Receive.
    private EndPoint _cachedEndPoint = new IPEndPoint(IPAddress.Any, 0);

    public void Poll()
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
                PollInGame();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PollInLobby()
    {
        var receivedSpan = _messenger.Receive(ref _cachedEndPoint);
        if (receivedSpan.Length == 0) return;

        var packetType = (PacketType)receivedSpan[0];

        switch (packetType)
        {
            case PacketType.Hello:
                if (_messenger.Peers.Count >= NetMessenger.MaxPeers)
                {
                    Console.WriteLine("Failed to accept new peer, max peers already reached!");
                    return;
                }

                // If the peer doesn't receive acknowledgement they will keep saying hello,
                // until an acknowledgement is successfully received.
                _messenger.SendAcknowledgement(PacketType.Hello, _cachedEndPoint);
                _messenger.Peers.Add(_cachedEndPoint);

                break;
            default:
                Console.WriteLine($"Got invalid packet, #{receivedSpan[0]}!");
                break;
        }
    }

    private void PollStartingGame()
    {

    }

    private void PollInGame()
    {
        // var receivedSpan = Receive(ref _cachedEndPoint);
        // if (receivedSpan.Length == 0) return;
        //
        // var packetType = (PacketType)receivedSpan[0];
        //
        // switch (packetType)
        // {
        //     case PacketType.Message:
        //         var sender = _cachedEndPoint == _hostEndPoint ? "host peer" : "other peer";
        //         var messageSpan = new Span<byte>(_buffer, 1, receivedSpan.Length - 1);
        //         var receivedMessage = Encoding.ASCII.GetString(messageSpan);
        //         Console.WriteLine($"Received from {sender}: {receivedMessage}");
        //         break;
        //     default:
        //         Console.WriteLine($"Got invalid packet, #{receivedSpan[0]}!");
        //         break;
        // }
    }

    public void SendMessageToAll(string message)
    {
        _messenger.Buffer[0] = (byte)PacketType.Message;
        Encoding.ASCII.GetBytes(message, new Span<byte>(_messenger.Buffer, 1, message.Length));
        _messenger.SendFromBuffer(message.Length + 1);
    }

    private void SendAddPeersToAll()
    {
        _messenger.Buffer[0] = (byte)PacketType.AddPeers;
        _messenger.Buffer[1] = (byte)_messenger.Peers.Count;

        var currentOffset = 2;

        foreach (var peer in _messenger.Peers)
        {
            var bytesWritten = WritePeerToBuffer(peer, currentOffset);

            if (bytesWritten == 0)
            {
                // Failed to write the peer to the buffer.
                return;
            }

            currentOffset += bytesWritten;
        }

        _messenger.SendFromBuffer(currentOffset);
    }

    private int WritePeerToBuffer(EndPoint peer, int offset)
    {
        if (peer is not IPEndPoint ipEndPoint)
        {
            Console.WriteLine($"Peer couldn't be converted to an IP endpoint: {peer}");
            return 0;
        }

        var ipEndPointString = ipEndPoint.ToString();

        _messenger.Buffer[offset] = (byte)ipEndPointString.Length;
        offset++;

        var destinationSpan = new Span<byte>(_messenger.Buffer, offset, _messenger.Buffer.Length - offset);
        Encoding.ASCII.GetBytes(ipEndPointString, destinationSpan);

        return ipEndPointString.Length + 1;
    }

    public void Dispose()
    {
        _messenger.Dispose();
    }
}