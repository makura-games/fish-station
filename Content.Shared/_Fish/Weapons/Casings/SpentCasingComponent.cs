namespace Content.Shared._Fish.Weapons.Casings;

/// <summary>
/// Стреляная гильза. Таймер исчезновения считается от выстрела,
/// но <see cref="Robust.Shared.Spawners.TimedDespawnComponent"/> вешается только когда гильза
/// уже не лежит в контейнере оружия.
/// </summary>
[RegisterComponent]
public sealed partial class SpentCasingComponent : Component
{
    /// <summary>
    /// Через сколько секунд после выстрела гильза исчезает.
    /// При автоматической пометке сюда копируется CVar <c>fish.casing_despawn_seconds</c>.
    /// </summary>
    [DataField]
    public float DespawnSeconds = 60f;

    /// <summary>
    /// Момент выстрела. Нужен, чтобы выброс из барабана или помпы не продлевал жизнь гильзы.
    /// </summary>
    public TimeSpan SpentAt;
}
