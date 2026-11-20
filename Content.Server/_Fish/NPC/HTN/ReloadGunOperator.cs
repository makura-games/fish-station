using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;

namespace Content.Server._Fish.NPC.HTN;

/// <summary>
/// Пытается перезарядить оружие NPC из рук/инвентаря (магазин или ballistic ammo).
/// </summary>
public sealed partial class ReloadGunOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var gunSystem = _entManager.System<GunSystem>();
        var inventory = _entManager.System<InventorySystem>();
        var slots = _entManager.System<ItemSlotsSystem>();
        var whitelist = _entManager.System<EntityWhitelistSystem>();
        var interaction = _entManager.System<SharedInteractionSystem>();

        if (!gunSystem.TryGetGun(owner, out var gun))
            return HTNOperatorStatus.Failed;

        var ammoEv = new GetAmmoCountEvent();
        _entManager.EventBus.RaiseLocalEvent(gun.Owner, ref ammoEv);
        if (ammoEv.Capacity > 0 && ammoEv.Count / (float) ammoEv.Capacity > 0.001f)
            return HTNOperatorStatus.Finished;

        // Магазинное оружие: вставить магазин из инвентаря.
        if (_entManager.HasComponent<MagazineAmmoProviderComponent>(gun.Owner) ||
            _entManager.HasComponent<ChamberMagazineAmmoProviderComponent>(gun.Owner))
        {
            foreach (var item in inventory.GetHandOrInventoryEntities(owner))
            {
                if (item == gun.Owner)
                    continue;

                if (!slots.TryInsert(gun.Owner, SharedGunSystem.MagazineSlot, item, owner, excludeUserAudio: true))
                    continue;

                return HTNOperatorStatus.Finished;
            }
        }

        // Ballistic: InteractUsing патроном/коробкой по оружию.
        if (_entManager.TryGetComponent(gun.Owner, out BallisticAmmoProviderComponent? ballistic))
        {
            var gunCoords = _entManager.GetComponent<TransformComponent>(gun.Owner).Coordinates;
            foreach (var item in inventory.GetHandOrInventoryEntities(owner))
            {
                if (item == gun.Owner)
                    continue;

                if (whitelist.IsWhitelistFail(ballistic.Whitelist, item))
                    continue;

                interaction.InteractUsing(owner, item, gun.Owner, gunCoords, checkCanInteract: false, checkCanUse: false);
                return HTNOperatorStatus.Finished;
            }
        }

        return HTNOperatorStatus.Failed;
    }
}
