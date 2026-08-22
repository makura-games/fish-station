using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Внутренние отказы: ролл при уроне, DriveFault рысканье, ремонт инструментами.
/// </summary>
public abstract class SharedMechInternalDamageSystem : EntitySystem
{
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedMechSystem Mech = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechInternalDamageComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MechInternalDamageComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<MechInternalDamageComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnDamageChanged(Entity<MechInternalDamageComponent> ent, ref DamageChangedEvent args)
    {
        // Рандомный Dirty только на сервере — иначе prediction reset / mispredict.
        if (Net.IsClient)
            return;

        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (!TryComp(ent, out MechComponent? mech) || mech.MaxIntegrity <= 0)
            return;

        var ratio = (mech.Integrity / mech.MaxIntegrity).Float();
        // Ролл только когда корпус уже заметно побит (ниже порога целостности).
        if (ratio > ent.Comp.IntegrityThreshold)
            return;

        if (!Random.Prob(ent.Comp.DamageChance))
            return;

        var flag = PickDamageFlag(args);
        if ((ent.Comp.Damage & flag) == flag)
            return;

        ent.Comp.Damage |= flag;
        Dirty(ent);

        if (mech.PilotSlot.ContainedEntity is { } pilot)
            Popup.PopupEntity(Loc.GetString("mech-internal-damage-applied"), ent, pilot);
    }

    private MechInternalDamageFlags PickDamageFlag(DamageChangedEvent args)
    {
        FixedPoint2 heat = FixedPoint2.Zero;
        FixedPoint2 blunt = FixedPoint2.Zero;
        if (args.DamageDelta != null)
        {
            args.DamageDelta.DamageDict.TryGetValue("Heat", out heat);
            args.DamageDelta.DamageDict.TryGetValue("Blunt", out blunt);
        }

        if (heat > blunt)
            return MechInternalDamageFlags.CabinFire | MechInternalDamageFlags.CoolantFail;

        if (blunt > FixedPoint2.Zero && Random.Prob(0.45f))
            return MechInternalDamageFlags.DriveFault;

        if (Random.Prob(0.4f))
            return MechInternalDamageFlags.PowerSpike;

        return MechInternalDamageFlags.HullBreach;
    }

    private void OnMoveInput(Entity<MechInternalDamageComponent> ent, ref MoveInputEvent args)
    {
        if (Net.IsClient)
            return;

        if ((ent.Comp.Damage & MechInternalDamageFlags.DriveFault) == 0)
            return;

        if (!args.HasDirectionalMovement)
            return;

        if (Random.Prob(0.28f))
            _transform.SetLocalRotation(ent, Random.NextAngle());
    }

    private void OnInteractUsing(Entity<MechInternalDamageComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if ((ent.Comp.Damage & MechInternalDamageFlags.PowerSpike) != 0 &&
            _tools.HasQuality(args.Used, CuttingQuality))
        {
            ClearFlag(ent, MechInternalDamageFlags.PowerSpike);
            args.Handled = true;
            Popup.PopupClient(Loc.GetString("mech-internal-damage-repaired-power"), ent, args.User);
            return;
        }

        if ((ent.Comp.Damage & (MechInternalDamageFlags.HullBreach | MechInternalDamageFlags.CabinFire |
                                MechInternalDamageFlags.CoolantFail)) != 0 &&
            _tools.HasQuality(args.Used, WeldingQuality))
        {
            ClearFlag(ent,
                MechInternalDamageFlags.HullBreach | MechInternalDamageFlags.CabinFire |
                MechInternalDamageFlags.CoolantFail);
            args.Handled = true;
            Popup.PopupClient(Loc.GetString("mech-internal-damage-repaired-hull"), ent, args.User);
            return;
        }

        if ((ent.Comp.Damage & MechInternalDamageFlags.DriveFault) != 0 &&
            _tools.HasQuality(args.Used, CuttingQuality))
        {
            ClearFlag(ent, MechInternalDamageFlags.DriveFault);
            args.Handled = true;
            Popup.PopupClient(Loc.GetString("mech-internal-damage-repaired-drive"), ent, args.User);
        }
    }

    public void ClearFlag(Entity<MechInternalDamageComponent> ent, MechInternalDamageFlags flags)
    {
        ent.Comp.Damage &= ~flags;
        Dirty(ent);
    }
}
