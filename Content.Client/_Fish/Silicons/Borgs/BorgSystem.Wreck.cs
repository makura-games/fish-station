using Content.Shared.Mobs;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Fish: переключение Body-слоя на wreck-состояние борга Mk2 в критическом/мёртвом
/// состоянии и обратно при возврате в Alive. Вызывается из ванильного
/// OnBorgAppearanceChanged одной строкой (дублирующая подписка запрещена EventBus).
/// </summary>
public sealed partial class BorgSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// Переключает Body-слой на wreck-состояние борга Mk2 в критическом/мёртвом
    /// состоянии и обратно при возврате в Alive. Wreck-спрайт есть только у типов,
    /// задавших <see cref="BorgTypePrototype.SpriteWreckState"/>.
    /// Также управляет слоем фонаря (LightStatus): в критическом/мёртвом состоянии слой
    /// скрыт независимо от данных, а при возврате в Alive видимость читается напрямую
    /// из ToggleableVisuals.Enabled в appearance — так слой синхронен с фактическим
    /// состоянием фонаря и не зависит от порядка обработчиков AppearanceChangeEvent.
    /// </summary>
    private void UpdateBorgWreckAppearance(Entity<BorgChassisComponent> chassis, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<BorgSwitchableTypeComponent>(chassis, out var switchable))
            return;

        if (switchable.SelectedBorgType is not { } borgType)
            return;

        if (!_prototype.TryIndex(borgType, out var prototype) || prototype.SpriteWreckState == null)
            return;

        if (!_appearance.TryGetData<MobState>(chassis.Owner, MobStateVisuals.State, out var state, args.Component))
            return;

        // При возврате в Alive восстанавливаем исходный Body-состояние типа;
        // в критическом/мёртвом переключаемся на wreck-кадр.
        var bodyState = state == MobState.Alive
            ? prototype.SpriteBodyState
            : prototype.SpriteWreckState;

        _sprite.LayerSetRsiState((chassis.Owner, args.Sprite), BorgVisualLayers.Body, bodyState!);

        var lightVisible = false;
        if (state == MobState.Alive)
        {
            _appearance.TryGetData<bool>(
                chassis.Owner,
                ToggleableVisuals.Enabled,
                out lightVisible,
                args.Component);
        }

        _sprite.LayerSetVisible((chassis.Owner, args.Sprite), BorgVisualLayers.LightStatus, lightVisible);
    }
}
