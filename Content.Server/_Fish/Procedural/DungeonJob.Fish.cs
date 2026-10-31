#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// Максимальное расстояние от целевой точки, на которое данж может оказаться после <c>TryFTLProximity</c>.
    /// Если данж унесло дальше, он возвращается в целевую точку.
    /// </summary>
    private const float MaxSpawnDistance = 1500f;
}
