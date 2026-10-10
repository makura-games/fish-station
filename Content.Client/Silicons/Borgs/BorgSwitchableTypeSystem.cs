// Fish-edit
using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Client side logic for borg type switching. Sets up primarily client-side visual information.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
public sealed partial class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private BorgSystem _borgSystem = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, AfterAutoHandleStateEvent>(AfterStateHandler);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<BorgSwitchableTypeComponent> ent, ref ComponentStartup args)
    {
        UpdateEntityAppearance(ent);
    }

    private void AfterStateHandler(Entity<BorgSwitchableTypeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateEntityAppearance(ent);
    }

    protected override void UpdateEntityAppearance(
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype)
    {
        if (TryComp(entity, out SpriteComponent? sprite))
        {
            // Fish-Start
            // Mk2 рисуется из отдельного холста (_Lust), а RSI слоёв у живой сущности
            // остался от родительского chassis. Меняем его до выставления state, иначе
            // state вроде sec_mk2* будет искаться в vanilla-RSI и даст ERRO.
            if (prototype.SpriteRsiPath is { } rsiPath)
            {
                _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.Body, rsiPath);
                _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.Light, rsiPath);
                _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.LightStatus, rsiPath);
            }

            // Сдвиг уходит на корень спрайта: так runtime-слои предметов (in-hand,
            // надетая экипировка) наследуют сдвиг и едут вместе с телом, а не отстают.
            // Присваиваем безусловно, чтобы сбросить сдвиг у типов без него.
            _sprite.SetOffset((entity.Owner, sprite), prototype.SpriteOffset ?? Vector2.Zero);
            // Fish-End

            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.Body, prototype.SpriteBodyState);
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.LightStatus, prototype.SpriteToggleLightState);
        }

        if (TryComp(entity, out BorgChassisComponent? chassis))
        {
            _borgSystem.SetMindStates(
                (entity.Owner, chassis),
                prototype.SpriteHasMindState,
                prototype.SpriteNoMindState);

            if (TryComp(entity, out AppearanceComponent? appearance))
            {
                // Queue update so state changes apply.
                _appearance.QueueUpdate(entity, appearance);
            }
        }

        base.UpdateEntityAppearance(entity, prototype);
    }
}
