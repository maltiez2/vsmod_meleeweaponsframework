﻿using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace MeleeWeaponsFramework;

public readonly struct Angle
{
    public float Radians => _value;
    public float Degrees => _value * GameMath.RAD2DEG;
    public float Minutes => _value * GameMath.RAD2DEG * 60f;
    public float Seconds => _value * GameMath.RAD2DEG * 3600f;

    public override string ToString() => $"{_value * GameMath.RAD2DEG:F2} deg";
    public override bool Equals(object? obj) => ((Angle?)obj)?._value == _value;
    public override int GetHashCode() => _value.GetHashCode();

    public static Angle Zero => new(0);

    public static Angle FromRadians(float radians) => new(radians);
    public static Angle FromDegrees(float degrees) => new(degrees * GameMath.DEG2RAD);
    public static Angle FromMinutes(float minutes) => new(minutes * GameMath.DEG2RAD / 60f);
    public static Angle FromSeconds(float seconds) => new(seconds * GameMath.DEG2RAD / 3600f);

    public static Angle operator +(Angle a, Angle b) => new(a._value + b._value);
    public static Angle operator -(Angle a, Angle b) => new(a._value - b._value);
    public static Angle operator *(Angle a, float b) => new(a._value * b);
    public static Angle operator *(float a, Angle b) => new(a * b._value);
    public static Angle operator /(Angle a, float b) => new(a._value / b);
    public static float operator /(Angle a, Angle b) => a._value / b._value;

    public static bool operator ==(Angle a, Angle b) => MathF.Abs(a._value - b._value) < Epsilon(a._value, b._value);
    public static bool operator !=(Angle a, Angle b) => MathF.Abs(a._value - b._value) >= Epsilon(a._value, b._value);
    public static bool operator <(Angle a, Angle b) => a._value < b._value && a != b;
    public static bool operator >(Angle a, Angle b) => a._value > b._value && a != b;
    public static bool operator <=(Angle a, Angle b) => a._value <= b._value;
    public static bool operator >=(Angle a, Angle b) => a._value >= b._value;

    private Angle(float radians) => _value = radians;
    private readonly float _value;

    /// <summary>
    /// For ~1% precision when dealing with seconds
    /// </summary>
    private const float _epsilonFactor = 1e-8f;
    private static float Epsilon(float a, float b) => MathF.Max(Math.Abs(a), Math.Abs(b)) * _epsilonFactor;
}

public readonly struct DirectionOffset
{
    public readonly Angle Pitch;
    public readonly Angle Yaw;

    public DirectionOffset(Vec3d direction, Vec3d reference)
    {
        float[] from = new[] { (float)reference.X, (float)reference.Y, (float)reference.Z };
        float[] to = new[] { (float)direction.X, (float)direction.Y, (float)direction.Z };

        float yawSin = (from[2] * to[0] - from[0] * to[2]) / MathF.Sqrt((from[0] * from[0] + from[2] * from[2]) * (to[0] * to[0] + to[2] * to[2]));
        float pitchSin = (from[2] * to[1] - from[1] * to[2]) / MathF.Sqrt((from[1] * from[1] + from[2] * from[2]) * (to[1] * to[1] + to[2] * to[2]));
        Yaw = Angle.FromRadians(MathF.Asin(yawSin));
        Pitch = Angle.FromRadians(MathF.Asin(pitchSin));
    }
    public DirectionOffset(Vec3f direction, Vec3f reference)
    {
        float yawSin = (reference.Z * direction.X - reference.X * direction.Z) / MathF.Sqrt((reference.X * reference.X + reference.Z * reference.Z) * (direction.X * direction.X + direction.Z * direction.Z));
        float pitchSin = (reference.Z * direction.Y - reference.Y * direction.Z) / MathF.Sqrt((reference.Y * reference.Y + reference.Z * reference.Z) * (direction.Y * direction.Y + direction.Z * direction.Z));
        Yaw = Angle.FromRadians(MathF.Asin(yawSin));
        Pitch = Angle.FromRadians(MathF.Asin(pitchSin));
    }
    public DirectionOffset(Angle pitch, Angle yaw)
    {
        Yaw = yaw;
        Pitch = pitch;
    }

    public override readonly string ToString() => $"Pitch: {Pitch}, Yaw: {Yaw}";
    public override bool Equals(object? obj) => ((DirectionOffset?)obj)?.Pitch == Pitch && ((DirectionOffset)obj).Yaw == Yaw;
    public override int GetHashCode() => (Pitch, Yaw).GetHashCode();

    public static DirectionOffset Zero => new(Angle.Zero, Angle.Zero);

    public static DirectionOffset FromRadians(float pitch, float yaw) => new(Angle.FromRadians(pitch), Angle.FromRadians(yaw));
    public static DirectionOffset FromDegrees(float pitch, float yaw) => new(Angle.FromDegrees(pitch), Angle.FromDegrees(yaw));
    public static DirectionOffset FromMinutes(float pitch, float yaw) => new(Angle.FromMinutes(pitch), Angle.FromMinutes(yaw));
    public static DirectionOffset FromSeconds(float pitch, float yaw) => new(Angle.FromSeconds(pitch), Angle.FromSeconds(yaw));

    public static DirectionOffset operator +(DirectionOffset a, DirectionOffset b) => new(a.Pitch + b.Pitch, a.Yaw + b.Yaw);
    public static DirectionOffset operator -(DirectionOffset a, DirectionOffset b) => new(a.Pitch - b.Pitch, a.Yaw - b.Yaw);
    public static DirectionOffset operator *(DirectionOffset a, float b) => new(a.Pitch * b, a.Yaw * b);
    public static DirectionOffset operator *(float a, DirectionOffset b) => new(a * b.Pitch, a * b.Yaw);
    public static DirectionOffset operator /(DirectionOffset a, float b) => new(a.Pitch / b, a.Yaw / b);

    public static bool operator ==(DirectionOffset a, DirectionOffset b) => a.Pitch == b.Pitch && a.Yaw == b.Yaw;
    public static bool operator !=(DirectionOffset a, DirectionOffset b) => !(a == b);
    public static bool operator <(DirectionOffset a, DirectionOffset b) => a.Pitch < b.Pitch && a.Yaw < b.Yaw;
    public static bool operator >(DirectionOffset a, DirectionOffset b) => a.Pitch > b.Pitch && a.Yaw > b.Yaw;
    public static bool operator <=(DirectionOffset a, DirectionOffset b) => !(a > b);
    public static bool operator >=(DirectionOffset a, DirectionOffset b) => !(a < b);

    static public Vec3f FromCameraReferenceFrame(EntityAgent player, Vec3f position)
    {
        Vec3f viewVector = player.SidedPos.GetViewVector().Normalize();
        Vec3f vertical = new(0, 1, 0);
        Vec3f localZ = viewVector;
        Vec3f localX = viewVector.Cross(vertical).Normalize();
        Vec3f localY = localX.Cross(localZ);
        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public Vec3d FromCameraReferenceFrame(EntityAgent player, Vec3d position)
    {
        Vec3f viewVectorF = player.SidedPos.GetViewVector();
        Vec3d viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
        Vec3d vertical = new(0, 1, 0);
        Vec3d localZ = viewVector.Normalize();
        Vec3d localX = viewVector.Cross(vertical).Normalize();
        Vec3d localY = localX.Cross(localZ);
        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public Vec3d ToCameraReferenceFrame(EntityAgent player, Vec3d position)
    {
        Vec3f viewVectorF = player.SidedPos.GetViewVector();
        Vec3d viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
        Vec3d vertical = new(0, 1, 0);
        Vec3d localZ = viewVector.Normalize();
        Vec3d localX = viewVector.Cross(vertical).Normalize();
        Vec3d localY = localX.Cross(localZ);

        InverseMatrix(localX, localY, localZ);

        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public Vec3f ToCameraReferenceFrame(EntityAgent player, Vec3f position)
    {
        Vec3f viewVectorF = player.SidedPos.GetViewVector();
        Vec3f viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
        Vec3f vertical = new(0, 1, 0);
        Vec3f localZ = viewVector.Normalize();
        Vec3f localX = viewVector.Cross(vertical).Normalize();
        Vec3f localY = localX.Cross(localZ);

        InverseMatrix(localX, localY, localZ);

        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public Vec3d ToReferenceFrame(Vec3d reference, Vec3d position)
    {
        Vec3d vertical = new(0, 1, 0);
        Vec3d localZ = reference.Normalize();
        Vec3d localX = reference.Cross(vertical).Normalize();
        Vec3d localY = localX.Cross(localZ);

        InverseMatrix(localX, localY, localZ);

        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public Vec3f ToReferenceFrame(Vec3f reference, Vec3f position)
    {
        Vec3f vertical = new(0, 1, 0);
        Vec3f localZ = reference.Normalize();
        Vec3f localX = reference.Cross(vertical).Normalize();
        Vec3f localY = localX.Cross(localZ);

        InverseMatrix(localX, localY, localZ);

        return localX * position.X + localY * position.Y + localZ * position.Z;
    }
    static public void InverseMatrix(Vec3d X, Vec3d Y, Vec3d Z)
    {
        double[] matrix = { X.X, X.Y, X.Z, Y.X, Y.Y, Y.Z, Z.X, Z.Y, Z.Z };
        Mat3d.Invert(matrix, matrix);
        X.X = matrix[0];
        X.Y = matrix[1];
        X.Z = matrix[2];
        Y.X = matrix[3];
        Y.Y = matrix[4];
        Y.Z = matrix[5];
        Z.X = matrix[6];
        Z.Y = matrix[7];
        Z.Z = matrix[8];
    }
    static public void InverseMatrix(Vec3f X, Vec3f Y, Vec3f Z)
    {
        float[] matrix = { X.X, X.Y, X.Z, Y.X, Y.Y, Y.Z, Z.X, Z.Y, Z.Z };
        Mat3f.Invert(matrix, matrix);
        X.X = matrix[0];
        X.Y = matrix[1];
        X.Z = matrix[2];
        Y.X = matrix[3];
        Y.Y = matrix[4];
        Y.Z = matrix[5];
        Z.X = matrix[6];
        Z.Y = matrix[7];
        Z.Z = matrix[8];
    }
}

public readonly struct DirectionConstrain
{
    /// <summary>
    /// In radians. Positive direction: top.
    /// </summary>
    public readonly Angle PitchTop;
    /// <summary>
    /// In radians. Positive direction: top.
    /// </summary>
    public readonly Angle PitchBottom;
    /// <summary>
    /// In radians. Positive direction: right.
    /// </summary>
    public readonly Angle YawLeft;
    /// <summary>
    /// In radians. Positive direction: right.
    /// </summary>
    public readonly Angle YawRight;

    public DirectionConstrain(Angle pitchTop, Angle pitchBottom, Angle yawRight, Angle yawLeft)
    {
        PitchTop = pitchTop;
        PitchBottom = pitchBottom;
        YawLeft = yawLeft;
        YawRight = yawRight;
    }

    public static DirectionConstrain FromDegrees(float top, float bottom, float right, float left)
    {
        return new(Angle.FromDegrees(top), Angle.FromDegrees(-bottom), Angle.FromDegrees(right), Angle.FromDegrees(-left));
    }

    public static DirectionConstrain FromDegrees(float angle)
    {
        return new(Angle.FromDegrees(angle), Angle.FromDegrees(-angle), Angle.FromDegrees(angle), Angle.FromDegrees(-angle));
    }

    public bool Check(DirectionOffset offset)
    {
        return offset.Pitch <= PitchTop &&
            offset.Pitch >= PitchBottom &&
            offset.Yaw >= YawLeft &&
            offset.Yaw <= YawRight;
    }
}
