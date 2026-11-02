namespace Content.Shared.Atmos.Components;

/// <summary>
/// Marker component: the entity is immune to pressure (barotrauma).
/// </summary>
/// <remarks>
/// Fish-edit: перенесён из Content.Server в Content.Shared, иначе предсказанный метаболизм на клиенте
/// падал с UnknownComponentException, когда реагент выдавал статус-эффект PressureImmunity.
/// Файл оставлен в ванильном пути Content.Shared/Atmos/Components, так как сам компонент ванильный.
/// </remarks>
[RegisterComponent]
public sealed partial class PressureImmunityComponent : Component
{
}
