namespace DeterministicNetcode.Net;

public enum PacketType : byte
{
    Hello,
    Message,
    AddPeers,
    Acknowledge
}