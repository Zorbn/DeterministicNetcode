using System;
using Microsoft.Xna.Framework;

namespace DeterministicNetcode;

public class DeterministicGame
{
    private const int PlayerSpeed = 2;

    public Point WallPosition => _wallPosition;
    public Point PlayerPosition => _playerPosition;

    private Point _wallPosition = new(128, 128);
    private Point _wallSize = new(64, 64);

    private Point _playerPosition = new(0, 0);

    public void DeterministicStep(InputState inputState)
    {
        var playerMotionX = Math.Sign(inputState.AxisX) * PlayerSpeed;
        var playerMotionY = Math.Sign(inputState.AxisY) * PlayerSpeed;

        if (!IsColliding(_playerPosition.X + playerMotionX, _playerPosition.Y, 64, 64,
                _wallPosition.X, _wallPosition.Y, _wallSize.X, _wallSize.Y))
        {
            _playerPosition.X += playerMotionX;
        }

        if (!IsColliding(_playerPosition.X, _playerPosition.Y + playerMotionY, 64, 64,
                _wallPosition.X, _wallPosition.Y, _wallSize.X, _wallSize.Y))
        {
            _playerPosition.Y += playerMotionY;
        }
    }

    private static bool IsColliding(int x1, int y1, int width1, int height1, int x2, int y2, int width2, int height2)
    {
        return x1 < x2 + width2 && x1 + width1 > x2 && y1 < y2 + height2 && y1 + height1 > y2;
    }
}