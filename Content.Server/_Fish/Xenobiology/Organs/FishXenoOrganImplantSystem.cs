using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._Fish.Xenobiology.Organs;

/// <summary>
/// Эффект ксено-органа при имплантации / извлечении.
/// </summary>
public sealed partial class FishXenoOrganImplantSystem : EntitySystem
{
    [Dependency] private FishXenoOrganEffectSystem _effects = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenoOrganImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<FishXenoOrganImplantComponent, ImplantRemovedEvent>(OnRemoved);
    }

    private void OnImplanted(Entity<FishXenoOrganImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        _effects.ApplyOrganEffect(args.Implanted, ent.Comp.Kind, ent.Comp.ImplantDuration);
    }

    private void OnRemoved(Entity<FishXenoOrganImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (!TryComp<FishXenoOrganBuffComponent>(args.Implanted, out var buff) || buff.Kind != ent.Comp.Kind)
            return;

        if (ent.Comp.Kind == FishXenoOrganEffectKind.Vigor)
            _movement.RefreshMovementSpeedModifiers(args.Implanted);

        RemComp<FishXenoOrganBuffComponent>(args.Implanted);
    }
}
