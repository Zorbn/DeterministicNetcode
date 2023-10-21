namespace DeterministicNetcode.Net;

// Game creation flow:
// Host creates lobby,
// peers join by saying hello,
// host tries to start the game,
// host creates add peers message,
// host sends add peers message to all peers,
// host keeps resending until all peers have acknowledged or timeout is reached,
// any peers who haven't acknowledged get removed,
// the game can now begin.
public enum NetState
{
    InLobby,
    StartingGame,
    InGame
}