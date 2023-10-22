using System;
using DeterministicNetcode.Net;
using Microsoft.Xna.Framework;

namespace DeterministicNetcode;

public class DeterministicGame
{
    private const int PlayerSpeed = 2;

    public Point WallPosition => _wallPosition;
    public readonly Point[] PlayerPositions;

    private Point _wallPosition = new(128, 128);
    private Point _wallSize = new(64, 64);


    public DeterministicGame(int playerCount)
    {
        PlayerPositions = new Point[playerCount];
    }

    public void DeterministicStep(InputState[] inputStates)
    {
        for (var i = 0; i < PlayerPositions.Length; i++)
        {
            ref var inputState = ref inputStates[i];
            ref var playerPosition = ref PlayerPositions[i];

            var playerMotionX = Math.Sign(inputState.AxisX) * PlayerSpeed;
            var playerMotionY = Math.Sign(inputState.AxisY) * PlayerSpeed;

            if (!IsColliding(playerPosition.X + playerMotionX, playerPosition.Y, 64, 64,
                    _wallPosition.X, _wallPosition.Y, _wallSize.X, _wallSize.Y))
            {
                playerPosition.X += playerMotionX;
            }

            if (!IsColliding(playerPosition.X, playerPosition.Y + playerMotionY, 64, 64,
                    _wallPosition.X, _wallPosition.Y, _wallSize.X, _wallSize.Y))
            {
                playerPosition.Y += playerMotionY;
            }
        }
    }

    private static bool IsColliding(int x1, int y1, int width1, int height1, int x2, int y2, int width2, int height2)
    {
        return x1 < x2 + width2 && x1 + width1 > x2 && y1 < y2 + height2 && y1 + height1 > y2;
    }
}