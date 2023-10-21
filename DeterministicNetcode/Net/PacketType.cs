namespace DeterministicNetcode.Net;

public enum PacketType : byte
{
    Hello,
    AddPeers,
    Acknowledge,
    InputState
}