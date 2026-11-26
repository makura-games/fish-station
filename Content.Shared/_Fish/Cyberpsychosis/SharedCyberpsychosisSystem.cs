using System.Globalization;
using Content.Shared.Armor;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Cyberpsychosis;

/// <summary>Manages experimental cyberpsychosis accumulation, worn resistance, passive decay, and examine cues.</summary>
public sealed partial class SharedCyberpsychosisSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<CyberpsychosisResistanceComponent> _resistanceQuery = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberpsychosisComponent, ExaminedEvent>(OnExamineCyberpsychosis);
        SubscribeLocalEvent<CyberpsychosisResistanceComponent, ArmorExamineEvent>(OnArmorExamine);
        SubscribeLocalEvent<CyberpsychosisWandComponent, AfterInteractEvent>(OnWandAfterInteract);
        SubscribeLocalEvent<CyberpsychosisWandComponent, UseInHandEvent>(OnWandUseInHand);
        SubscribeLocalEvent<CyberpsychosisWandComponent, MeleeHitEvent>(OnWandMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer || _nextUpdate > _timing.CurTime)
            return;

        const float stepSeconds = 1f;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(stepSeconds);

        var query = EntityQueryEnumerator<CyberpsychosisComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Level <= 0f)
            {
                RemCompDeferred<CyberpsychosisComponent>(uid);
                continue;
            }

            comp.Level = MathF.Max(0f, comp.Level - comp.DecayPerSecond * stepSeconds);
            if (comp.Level <= 0f)
                RemCompDeferred<CyberpsychosisComponent>(uid);
            else
                Dirty(uid, comp);
        }
    }

    /// <summary>Checks whether an entity can have its cyberpsychosis level modified.</summary>
    public bool CanModifyCyberpsychosis(EntityUid target)
    {
        return !TerminatingOrDeleted(target) && _mobStateQuery.HasComp(target);
    }

    /// <summary>Modifies a character's cyberpsychosis level, applying worn resistance to positive buildup.</summary>
    public bool TryModifyCyberpsychosis(EntityUid target, float delta, out float newLevel)
    {
        newLevel = 0f;
        if (!CanModifyCyberpsychosis(target))
            return false;

        if (delta > 0f)
            delta *= GetResistanceMultiplier(target);

        var comp = EnsureComp<CyberpsychosisComponent>(target);
        comp.Level = Math.Clamp(comp.Level + delta, 0f, 100f);
        newLevel = comp.Level;

        if (comp.Level <= 0f)
            RemCompDeferred<CyberpsychosisComponent>(target);
        else
            Dirty(target, comp);

        return true;
    }

    /// <summary>Computes the combined cyberpsychosis damage multiplier from all worn equipment.</summary>
    public float GetResistanceMultiplier(EntityUid target)
    {
        var multiplier = 1f;
        var slots = _inventory.GetSlotEnumerator(target, SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item))
        {
            if (_resistanceQuery.TryComp(item, out var resist))
                multiplier *= Math.Clamp(resist.DamageCoefficient, 0f, 1f);
        }

        return multiplier;
    }

    private void OnWandAfterInteract(Entity<CyberpsychosisWandComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryUseWand(ent, args.User, target))
            args.Handled = true;
    }

    private void OnWandUseInHand(Entity<CyberpsychosisWandComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryUseWand(ent, args.User, args.User))
            args.Handled = true;
    }

    private void OnWandMeleeHit(Entity<CyberpsychosisWandComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
            TryUseWand(ent, args.User, target);
    }

    private bool TryUseWand(Entity<CyberpsychosisWandComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanModifyCyberpsychosis(target))
            return false;

        if (!_net.IsServer)
            return true;

        if (!TryModifyCyberpsychosis(target, ent.Comp.Delta, out var newLevel))
            return false;

        var formatted = newLevel.ToString("0.##", CultureInfo.InvariantCulture);
        _popup.PopupEntity(Loc.GetString("fish-cyberpsychosis-wand-applied", ("level", formatted)), target, user);
        return true;
    }

    private void OnExamineCyberpsychosis(Entity<CyberpsychosisComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Level < 20f)
            return;

        var target = Identity.Entity(ent.Owner, EntityManager);
        var locKey = ent.Comp.Level >= 90f
            ? "fish-cyberpsychosis-examine-high"
            : ent.Comp.Level >= 50f
                ? "fish-cyberpsychosis-examine-medium"
                : "fish-cyberpsychosis-examine-low";

        args.PushMarkup(Loc.GetString(locKey, ("target", target)));
    }

    private void OnArmorExamine(Entity<CyberpsychosisResistanceComponent> ent, ref ArmorExamineEvent args)
    {
        var percent = (int) MathF.Round((1f - Math.Clamp(ent.Comp.DamageCoefficient, 0f, 1f)) * 100f);
        if (percent <= 0)
            return;

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString("fish-cyberpsychosis-resistance-examine", ("percent", percent)));
    }
}
