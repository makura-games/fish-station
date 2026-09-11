using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

/// <summary>
/// Простая структура для хранения двумерных координат.
/// Используется в UI и сообщениях для указания цели блюспейс-артиллерии.
/// </summary>
[Serializable, NetSerializable]
public struct ArtilleryVector2
{
    /// <summary>
    /// Координата X (восток-запад).
    /// </summary>
    public float X;

    /// <summary>
    /// Координата Y (север-юг).
    /// </summary>
    public float Y;

    /// <summary>
    /// Создаёт новый вектор артиллерийских координат.
    /// </summary>
    public ArtilleryVector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Нулевой вектор (0,0).
    /// </summary>
    public static ArtilleryVector2 Zero => new(0, 0);

    public static ArtilleryVector2 operator +(ArtilleryVector2 a, ArtilleryVector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static ArtilleryVector2 operator -(ArtilleryVector2 a, ArtilleryVector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static ArtilleryVector2 operator *(ArtilleryVector2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static ArtilleryVector2 operator /(ArtilleryVector2 a, float scalar) => new(a.X / scalar, a.Y / scalar);

    /// <summary>
    /// Длина вектора.
    /// </summary>
    public float Length => MathF.Sqrt(X * X + Y * Y);

    /// <inheritdoc/>
    public override string ToString() => $"({X:F1}, {Y:F1})";
}