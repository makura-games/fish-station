using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Направленная броня Fish: сектора-конусы + абсолютные шансы рикошета.
/// В режиме обороны дополнительно режет входящий урон.
/// </summary>
public abstract class SharedMechFacingArmorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechFacingArmorComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<MechFacingArmorComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Damage.Empty)
            return;

        var side = MechFacingSide.Side;
        if (args.Origin is { } origin && origin != ent.Owner)
            side = GetFacingSide(ent, origin);

        var deflectChance = side switch
        {
            MechFacingSide.Front => ent.Comp.FrontDeflectChance,
            MechFacingSide.Back => ent.Comp.RearDeflectChance,
            _ => ent.Comp.SideDeflectChance,
        };

        // Случайный рикошет только на сервере — иначе mispredict / dirty в prediction.
        if (_net.IsServer && deflectChance > 0f && _random.Prob(Math.Clamp(deflectChance, 0f, 0.9f)))
        {
            args.Damage *= 0;
            _popup.PopupEntity(Loc.GetString("mech-facing-armor-deflect"), ent);
            return;
        }

        var coefficient = side switch
        {
            MechFacingSide.Front => ent.Comp.FrontDamageMult,
            MechFacingSide.Back => ent.Comp.RearDamageMult,
            _ => ent.Comp.SideDamageMult,
        };

        args.Damage *= coefficient;

        if (TryComp(ent, out MechDefenceModeComponent? defence) && defence.Active &&
            defence.DamageResistFraction > 0f)
        {
            args.Damage *= Math.Clamp(1f - defence.DamageResistFraction, 0.05f, 1f);
        }
    }

    public MechFacingSide GetFacingSide(Entity<MechFacingArmorComponent> ent, EntityUid origin)
    {
        var mechXform = Transform(ent);
        var originXform = Transform(origin);

        var mechPos = _transform.GetWorldPosition(mechXform);
        var originPos = _transform.GetWorldPosition(originXform);
        var delta = originPos - mechPos;
        if (delta.LengthSquared() < 0.0001f)
            return MechFacingSide.Side;

        var toOrigin = delta.ToWorldAngle();
        var facing = _transform.GetWorldRotation(mechXform);
        var absDeg = Math.Abs(Angle.ShortestDistance(facing, toOrigin).Degrees);

        if (absDeg <= ent.Comp.FrontConeHalfDegrees)
            return MechFacingSide.Front;

        if (absDeg >= 180f - ent.Comp.RearConeHalfDegrees)
            return MechFacingSide.Back;

        return MechFacingSide.Side;
    }
}

public enum MechFacingSide : byte
{
    Front,
    Side,
    Back,
}
