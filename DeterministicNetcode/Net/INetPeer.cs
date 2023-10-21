using System;

namespace DeterministicNetcode.Net;

public interface INetPeer : IDisposable
{
    public int Port { get; }

    public void Poll();
}