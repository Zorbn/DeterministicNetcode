using System;

namespace DeterministicNetcode.Net;

public struct InputState
{
    public const int SavedInputStateCount = 2;

    public int StepIndex;
    public int AxisX;
    public int AxisY;

    public int WriteBytes(byte[] buffer, int offset)
    {
        var startOffset = offset;

        var stepIndexDestination = new Span<byte>(buffer, offset, sizeof(int));
        BitConverter.TryWriteBytes(stepIndexDestination, StepIndex);
        offset += sizeof(int);

        var axisXDestination = new Span<byte>(buffer, offset, sizeof(int));
        BitConverter.TryWriteBytes(axisXDestination, AxisX);
        offset += sizeof(int);

        var axisYDestination = new Span<byte>(buffer, offset, sizeof(int));
        BitConverter.TryWriteBytes(axisYDestination, AxisY);
        offset += sizeof(int);

        return offset - startOffset;
    }

    public static InputState FromBytes(byte[] buffer, ref int offset)
    {
        InputState inputState;

        var stepIndexDestination = new Span<byte>(buffer, offset, sizeof(int));
        inputState.StepIndex = BitConverter.ToInt32(stepIndexDestination);
        offset += sizeof(int);

        var axisXDestination = new Span<byte>(buffer, offset, sizeof(int));
        inputState.AxisX = BitConverter.ToInt32(axisXDestination);
        offset += sizeof(int);

        var axisYDestination = new Span<byte>(buffer, offset, sizeof(int));
        inputState.AxisY = BitConverter.ToInt32(axisYDestination);
        offset += sizeof(int);

        return inputState;
    }
}