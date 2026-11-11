namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
///     Raised directed on the gun entity when ammo is shot to calculate its spread.
/// </summary>
/// <param name="Spread">The spread of the ammo, can be changed by handlers.</param>
/// <param name="Shooter">Сущность, выпустившая выстрел, если она известна.</param>
[ByRefEvent]
// Fish-edit: передаём стрелка для стрессового разброса.
public record struct GunGetAmmoSpreadEvent(Angle Spread, EntityUid? Shooter);
