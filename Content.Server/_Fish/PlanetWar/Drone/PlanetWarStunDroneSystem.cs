using Content.Server.Electrocution;
using Content.Server.Emp;
using Content.Server.Flash;
using Content.Shared.Flash;
using Content.Shared.Implants.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Trigger;
using Content.Shared._Fish.PlanetWar.Drone;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System;

namespace Content.Server._Fish.PlanetWar.Drone
{
    /// <summary>
    /// Handles PlanetWar stun drones triggering.
    /// </summary>
    public sealed class PlanetWarStunDroneSystem : EntitySystem
    {
        [Dependency] private readonly FlashSystem _flash = default!;
        [Dependency] private readonly EmpSystem _emp = default!;
        [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PlanetWarStunDroneComponent, TriggerEvent>(OnTrigger);
        }

        private void OnTrigger(EntityUid uid, PlanetWarStunDroneComponent component, ref TriggerEvent args)
        {
            // If this component is on an implant, get the host drone entity
            var droneUid = uid;
            if (TryComp<SubdermalImplantComponent>(uid, out var implant) && implant.ImplantedEntity != null)
            {
                droneUid = implant.ImplantedEntity.Value;
            }

            var coords = _transform.GetMapCoordinates(droneUid);

            // ===== FISH EDIT START: PV AMMO / WEAPON CHANGES =====
            // 1. Blind enemies (+7s к прежним 1s → 8s по умолчанию)
            // 3. Shock enemies in electrocution range
            var range5x5 = component.FlashRange;
            var range3x3 = component.ElectrocutionRange;
            var flashDuration = TimeSpan.FromSeconds(component.FlashDuration);
            // ===== FISH EDIT END: PV AMMO / WEAPON CHANGES =====

            // Find all potential targets with status effects
            var dronePos = _transform.GetWorldPosition(droneUid);
            foreach (var target in _lookup.GetEntitiesInRange<StatusEffectsComponent>(coords, range5x5))
            {
                if (target.Owner == droneUid)
                    continue;

                // Check if target is friendly to this drone
                if (_npcFaction.IsEntityFriendly(droneUid, target.Owner))
                    continue;

                // ===== FISH EDIT START: PV AMMO / WEAPON CHANGES =====
                _flash.Flash(target.Owner, droneUid, null, flashDuration, 0.8f, true);
                // ===== FISH EDIT END: PV AMMO / WEAPON CHANGES =====

                // If in 3x3 radius, shock them!
                var targetPos = _transform.GetWorldPosition(target.Owner);
                var distance = (targetPos - dronePos).Length();
                if (distance <= range3x3)
                {
                    // Electrocute: shock damage = 20, duration = 3 seconds (tesla shock effect)
                    _electrocution.TryDoElectrocution(target.Owner, droneUid, 20, TimeSpan.FromSeconds(3), true, ignoreInsulation: true);
                }
            }

            // 2. EMP pulse 3x3 (radius 1.5m)
            _emp.EmpPulse(coords, range3x3, 50000f, TimeSpan.FromSeconds(5));

            // Delete the drone since it has self-destructed/disoriented
            QueueDel(droneUid);
        }
    }
}
