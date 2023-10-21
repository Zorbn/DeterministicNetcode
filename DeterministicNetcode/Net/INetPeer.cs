using System;

namespace DeterministicNetcode.Net;

public interface INetPeer : IDisposable
{
    public bool IsHost { get; }
    public NetState State { get; }
    public NetMessenger Messenger { get; }

    public void Poll(int stepIndex);
}