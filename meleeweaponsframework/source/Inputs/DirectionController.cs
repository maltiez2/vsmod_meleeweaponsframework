﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using VSImGui.Debug;

namespace MeleeWeaponsFramework;

public enum DirectionsConfiguration
{
    None = 1,
    TopBottom = 2,
    Triangle = 3,
    Square = 4,
    Star = 5,
    Eight = 8
}

public enum AttackDirection
{
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    TopLeft
}

public readonly struct MouseMovementData
{
    public float Pitch { get; }
    public float Yaw { get; }
    public float DeltaPitch { get; }
    public float DeltaYaw { get; }

    public MouseMovementData(float pitch, float yaw, float deltaPitch, float deltaYaw)
    {
        Pitch = pitch;
        Yaw = yaw;
        DeltaPitch = deltaPitch;
        DeltaYaw = deltaYaw;
    }
}

public sealed class AttackDirectionController
{
    public DirectionsConfiguration DirectionsConfiguration { get; set; } = DirectionsConfiguration.Eight;
    public int Depth { get; set; } = 5;
    public float Sensitivity { get; set; } = 1.0f;
    public AttackDirection CurrentDirection { get; private set; }
    public int CurrentDirectionNormalized { get; private set; }

    public AttackDirectionController(ICoreClientAPI api, DirectionCursorRenderer renderer)
    {
        _api = api;
        _directionCursorRenderer = renderer;

        for (int count = 0; count < Depth * 2; count++)
        {
            _directionQueue.Enqueue(new(0, 0, 0, 0));
        }
    }

    public void OnGameTick()
    {
        if (DirectionsConfiguration == DirectionsConfiguration.None)
        {
            _directionCursorRenderer.Show = false;
            return;
        }

        _directionCursorRenderer.Show = true;

        float pitch = _api.Input.MousePitch;
        float yaw = _api.Input.MouseYaw;

        _directionQueue.Enqueue(new(pitch, yaw, pitch - _directionQueue.Last().Pitch, yaw - _directionQueue.Last().Yaw));

        MouseMovementData previous = _directionQueue.Dequeue();

        int direction = CalculateDirection(previous.Yaw - yaw, previous.Pitch - pitch, (int)DirectionsConfiguration);

        float delta = _directionQueue.Last().DeltaPitch * _directionQueue.Last().DeltaPitch + _directionQueue.Last().DeltaYaw * _directionQueue.Last().DeltaYaw;

        if (delta > _sensitivityFactor / Sensitivity)
        {
            CurrentDirectionNormalized = direction;
            CurrentDirection = (AttackDirection)_configurations[DirectionsConfiguration][CurrentDirectionNormalized];
            _directionCursorRenderer.CurrentDirection = (int)CurrentDirection;
        }
    }


    private const float _sensitivityFactor = 1e-5f;
    private readonly ICoreClientAPI _api;
    private readonly Queue<MouseMovementData> _directionQueue = new();
    private readonly DirectionCursorRenderer _directionCursorRenderer;
    private readonly Dictionary<DirectionsConfiguration, List<int>> _configurations = new()
    {
        { DirectionsConfiguration.TopBottom, new() {0, 4} },
        { DirectionsConfiguration.Triangle, new() {0, 3, 5} },
        { DirectionsConfiguration.Square, new() {0, 2, 4, 6} },
        { DirectionsConfiguration.Star, new() {0, 1, 3, 5, 7} },
        { DirectionsConfiguration.Eight, new() {0, 1, 2, 3, 4, 5, 6, 7, 8} }
    };

    private int CalculateDirection(float yaw, float pitch, int directionsCount)
    {
        float angleSegment = 360f / directionsCount;
        float directionOffset = angleSegment / 2f;
        float angle = MathF.Atan2(yaw, pitch) * GameMath.RAD2DEG;
        float angleOffset = angle + directionOffset + 360;
        return (int)(angleOffset / angleSegment) % directionsCount;
    }
}
