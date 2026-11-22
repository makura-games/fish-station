namespace Content.Shared._Fish.Weapons.Casings;

/// <summary>
/// Патрон переключили в spent или обратно в целый.
/// </summary>
[ByRefEvent]
public readonly record struct SpentAmmoChangedEvent(bool Spent);

/// <summary>
/// Гильзу или патрон выбросили из оружия.
/// </summary>
[ByRefEvent]
public readonly record struct SpentCasingEjectedEvent;
