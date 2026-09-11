using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.TimedDespawn;

/// <summary>
/// Убирает <see cref="Robust.Shared.Spawners.TimedDespawnComponent"/> при подборе в руку
/// или вставке в контейнер (инвентарь / хранилище).
/// </summary>
[RegisterComponent]
public sealed partial class CancelTimedDespawnOnInsertComponent : Component;
