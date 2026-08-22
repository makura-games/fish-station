using Content.Shared._Fish.Mechs;
using Content.Shared._Fish.Mechs.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mech.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Fish.Mechs;

/// <summary>
/// Robotics mech tracking console — список маяков.
/// </summary>
public sealed class MechTrackingConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<MechTrackingConsoleComponent>(MechTrackingUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<MechTrackingRefreshMessage>(OnRefresh);
        });
    }

    private void OnOpened(Entity<MechTrackingConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnRefresh(Entity<MechTrackingConsoleComponent> ent, ref MechTrackingRefreshMessage args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(EntityUid console)
    {
        var entries = new List<MechTrackingEntry>();
        var query = EntityQueryEnumerator<MechTrackingBeaconComponent, MechComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var mech, out var meta))
        {
            if (!beacon.Enabled)
                continue;

            var pilotName = string.Empty;
            if (mech.PilotSlot.ContainedEntity is { } pilot)
                pilotName = Identity.Name(pilot, EntityManager);

            var integrity = mech.MaxIntegrity > 0
                ? ((mech.MaxIntegrity - mech.Integrity) / mech.MaxIntegrity).Float()
                : 0f;
            var energy = mech.MaxEnergy > 0 ? (mech.Energy / mech.MaxEnergy).Float() : 0f;

            entries.Add(new MechTrackingEntry
            {
                Mech = GetNetEntity(uid),
                Name = meta.EntityName,
                IntegrityPercent = integrity * 100f,
                EnergyPercent = energy * 100f,
                PilotName = pilotName,
                Broken = mech.Broken,
            });
        }

        _ui.SetUiState(console, MechTrackingUiKey.Key, new MechTrackingBoundUserInterfaceState { Entries = entries });
    }
}
