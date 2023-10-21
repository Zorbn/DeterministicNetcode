using System;
using System.Net;

namespace DeterministicNetcode.Net;

public class NetPeer : INetPeer
{
    public int Port => _messenger.Port;

    private NetMessenger _messenger = new();
    private EndPoint _host;
    private NetState _state = NetState.InLobby;

    // Used to store the result of Receive.
    private EndPoint _cachedEndPoint = new IPEndPoint(IPAddress.Any, 0);

    public NetPeer(NetMessenger.Address hostAddress)
    {
        _host = NetMessenger.CreateIpEndPoint(hostAddress.Ip, hostAddress.Port);
        _messenger.Peers.Add(_host);
    }

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
        SendHelloToHost();

        var receivedSpan = _messenger.Receive(ref _cachedEndPoint);
        if (receivedSpan.Length == 0) return;

        if (_messenger.IsAcknowledgement(PacketType.Hello, receivedSpan))
        {
            _state = NetState.StartingGame;
            Console.WriteLine("Got ack for hello!");
        }
    }

    private void PollStartingGame()
    {
    }

    private void PollInGame()
    {

    }

    private void SendHelloToHost()
    {
        _messenger.Buffer[0] = (byte)PacketType.Hello;
        _messenger.SendFromBuffer(1, _host);
    }

    public void Dispose()
    {
        _messenger.Dispose();
    }
}