using Content.Shared._Fish.BorgRest;
using Content.Shared.Mobs;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130 // Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Fish: переключение Body-слоя на rest-состояние борга Mk2 во время отдыха и обратно.
/// Вызывается из ванильного OnBorgAppearanceChanged одной строкой (дублирующая подписка
/// запрещена EventBus), после wreck-визуала — у wreck приоритет над отдыхом.
/// </summary>
public sealed partial class BorgSystem
{
    /// <summary>
    /// Применяет rest-кадр тела и скрывает слои мозга/фонаря поверх него, пока борг
    /// отдыхает; при выходе самостоятельно восстанавливает тело и фонарь. Выход не
    /// делегируется wreck-обработчику: тот требует данные MobStateVisuals.State, которых
    /// у спавну-живого борга нет (ChangeState не срабатывает — состояние не покидалось),
    /// поэтому без своего восстановления тело навсегда оставалось бы на rest-кадре.
    /// Покоящийся кадр есть только у типов, задавших
    /// <see cref="BorgTypePrototype.SpriteRestingState"/>.
    /// </summary>
    private void UpdateBorgRestAppearance(Entity<BorgChassisComponent> chassis, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Управляем телом только при наличии данных отдыха.
        if (!_appearance.TryGetData<bool>(chassis.Owner, BorgRestVisuals.Resting, out var resting, args.Component))
            return;

        // Wreck-кадр имеет приоритет: сервер снимает отдых в крите/смерти, но порядок
        // событий appearance не гарантирован — на всякий случай не перекрываем его.
        // Отсутствие данных MobState трактуется как Alive: их нет до первой смены
        // состояния, а в крит wreck их обязательно выставляет.
        if (_appearance.TryGetData<MobState>(chassis.Owner, MobStateVisuals.State, out var state, args.Component)
            && state != MobState.Alive)
        {
            return;
        }

        if (!TryComp<BorgSwitchableTypeComponent>(chassis, out var switchable)
            || switchable.SelectedBorgType is not { } borgType
            || !_prototype.TryIndex(borgType, out var prototype)
            || prototype.SpriteRestingState == null)
        {
            return;
        }

        if (resting)
        {
            // Тело — rest-кадр; мозг и фонарь скрываются поверх покояющегося спрайта.
            _sprite.LayerSetRsiState((chassis.Owner, args.Sprite), BorgVisualLayers.Body, prototype.SpriteRestingState);
            _sprite.LayerSetVisible((chassis.Owner, args.Sprite), BorgVisualLayers.Light, false);
            _sprite.LayerSetVisible((chassis.Owner, args.Sprite), BorgVisualLayers.LightStatus, false);
            return;
        }

        // Выход: тело — исходный кадр типа, фонарь — по фактическому ToggleableVisuals.Enabled
        // (тот же источник, что у wreck-обработчика при возврате в Alive). Слой Light
        // восстанавливает ванильный визуал, вызвавший эту смену раньше нас в цепочке.
        if (prototype.SpriteBodyState != null)
            _sprite.LayerSetRsiState((chassis.Owner, args.Sprite), BorgVisualLayers.Body, prototype.SpriteBodyState);

        var lightVisible = _appearance.TryGetData<bool>(
            chassis.Owner,
            ToggleableVisuals.Enabled,
            out var enabled,
            args.Component) && enabled;
        _sprite.LayerSetVisible((chassis.Owner, args.Sprite), BorgVisualLayers.LightStatus, lightVisible);
    }
}
