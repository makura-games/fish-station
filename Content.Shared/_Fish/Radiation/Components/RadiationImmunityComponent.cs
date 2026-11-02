namespace Content.Shared.Radiation.Components;

/// <summary>
/// Полный иммунитет к облучению: сущность не накапливает радиационный урон от источников радиации.
/// </summary>
/// <remarks>
/// Компонент блокирует только НОВОЕ облучение (<see cref="Content.Shared.Radiation.Events.OnIrradiatedEvent"/>).
/// Уже накопленный радиационный урон он не снимает — для этого нужны обычные препараты.
/// Используется газом Крипиум.
/// </remarks>
[RegisterComponent]
public sealed partial class RadiationImmunityComponent : Component
{
}
