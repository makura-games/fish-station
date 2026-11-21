using Content.Shared._Starlight.Weapon.Components;
using Content.Shared.CCVar;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Weapons.Casings;

/// <summary>
/// Стреляные гильзы исчезают через заданное время после выстрела и не подбираются.
/// Целые патроны, в том числе выброшенные досылом, не затрагиваются.
/// </summary>
public sealed partial class SpentCasingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const float MinLifetimeSeconds = 0.01f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartridgeAmmoComponent, SpentAmmoChangedEvent>(OnCartridgeSpentChanged);
        SubscribeLocalEvent<CartridgeAmmoComponent, SpentCasingEjectedEvent>(OnCartridgeEjected);
        SubscribeLocalEvent<CartridgeAmmoComponent, MapInitEvent>(OnCartridgeMapInit);
        SubscribeLocalEvent<CartridgeAmmoComponent, GettingPickedUpAttemptEvent>(OnCartridgePickup);

        SubscribeLocalEvent<HitScanCartridgeAmmoComponent, SpentAmmoChangedEvent>(OnHitscanSpentChanged);
        SubscribeLocalEvent<HitScanCartridgeAmmoComponent, SpentCasingEjectedEvent>(OnHitscanEjected);
        SubscribeLocalEvent<HitScanCartridgeAmmoComponent, MapInitEvent>(OnHitscanMapInit);
        SubscribeLocalEvent<HitScanCartridgeAmmoComponent, GettingPickedUpAttemptEvent>(OnHitscanPickup);

        SubscribeLocalEvent<SpentCasingComponent, EntGotRemovedFromContainerMessage>(OnRemovedFromContainer);
    }

    private void OnCartridgeSpentChanged(Entity<CartridgeAmmoComponent> ent, ref SpentAmmoChangedEvent args)
    {
        if (args.Spent)
        {
            if (!ent.Comp.DeleteOnSpawn)
                MarkSpent(ent);
            return;
        }

        ClearSpent(ent);
    }

    private void OnHitscanSpentChanged(Entity<HitScanCartridgeAmmoComponent> ent, ref SpentAmmoChangedEvent args)
    {
        if (args.Spent)
        {
            if (!ent.Comp.DeleteOnSpawn)
                MarkSpent(ent);
            return;
        }

        ClearSpent(ent);
    }

    private void OnCartridgeEjected(Entity<CartridgeAmmoComponent> ent, ref SpentCasingEjectedEvent args)
    {
        if (!ent.Comp.Spent || ent.Comp.DeleteOnSpawn)
            return;

        MarkSpent(ent);
    }

    private void OnHitscanEjected(Entity<HitScanCartridgeAmmoComponent> ent, ref SpentCasingEjectedEvent args)
    {
        if (!ent.Comp.Spent || ent.Comp.DeleteOnSpawn)
            return;

        MarkSpent(ent);
    }

    private void OnCartridgeMapInit(Entity<CartridgeAmmoComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spent && !ent.Comp.DeleteOnSpawn)
            MarkSpent(ent);
    }

    private void OnHitscanMapInit(Entity<HitScanCartridgeAmmoComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spent && !ent.Comp.DeleteOnSpawn)
            MarkSpent(ent);
    }

    private void OnCartridgePickup(Entity<CartridgeAmmoComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Spent)
            return;

        CancelPickup(ent, ref args);
    }

    private void OnHitscanPickup(Entity<HitScanCartridgeAmmoComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Spent)
            return;

        CancelPickup(ent, ref args);
    }

    private void OnRemovedFromContainer(Entity<SpentCasingComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        TryArmDespawn(ent);
    }

    private void CancelPickup(EntityUid uid, ref GettingPickedUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();

        if (!args.ShowPopup)
            return;

        _popup.PopupEntity(Loc.GetString("fish-spent-casing-no-pickup"), uid, args.User);
    }

    /// <summary>
    /// Помечает гильзу как стреляную и, если она уже в мире, запускает таймер.
    /// </summary>
    private void MarkSpent(EntityUid uid)
    {
        if (!_net.IsServer || TerminatingOrDeleted(uid))
            return;

        if (!TryComp<SpentCasingComponent>(uid, out var spent))
        {
            spent = AddComp<SpentCasingComponent>(uid);
            spent.DespawnSeconds = Math.Max(0f, _cfg.GetCVar(FishCVars.CasingDespawnSeconds));
            // Текущая пауза ещё не попала в AutoPausedField: её вычитаем сразу.
            spent.SpentAt = _timing.CurTime - _metadata.GetPauseTime(uid);
        }

        TryArmDespawn((uid, spent));
    }

    private void ClearSpent(EntityUid uid)
    {
        if (!_net.IsServer || !RemComp<SpentCasingComponent>(uid))
            return;

        RemComp<TimedDespawnComponent>(uid);
    }

    private void TryArmDespawn(Entity<SpentCasingComponent> ent)
    {
        if (!_net.IsServer || TerminatingOrDeleted(ent) || _container.IsEntityInContainer(ent))
            return;

        var elapsed = (float)(_timing.CurTime - ent.Comp.SpentAt - _metadata.GetPauseTime(ent)).TotalSeconds;
        var remaining = ent.Comp.DespawnSeconds - elapsed;
        if (remaining < MinLifetimeSeconds)
            remaining = MinLifetimeSeconds;

        if (!TryComp<TimedDespawnComponent>(ent, out var timed))
        {
            timed = AddComp<TimedDespawnComponent>(ent);
            timed.Lifetime = remaining;
            return;
        }

        if (timed.Lifetime > remaining)
            timed.Lifetime = remaining;
    }
}
