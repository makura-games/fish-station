using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem
{
    private static readonly ProtoId<TagPrototype> CableCoilTag = "CableCoil";

    private static readonly ProtoId<DamageTypePrototype>[] IpcMechanicalTypes =
    [
        "Blunt",
        "Slash",
        "Piercing",
    ];

    // Fish-start: органика не лечит КПБ; кабель и нанопаста со своим списком контейнеров — лечат
    private static bool IpcAllowsHeal(HealingComponent healing)
    {
        if (healing.DamageContainers is not { Count: > 0 } containers)
            return false;

        foreach (var container in containers)
        {
            if (container == "Ipc"
                || container == "Silicon"
                || container == "Synth"
                || container == "Xenoborg"
                || container == "Mech")
                return true;
        }

        return false;
    }

    // Fish-start: катушка не лечит мех. урон КПБ
    private bool IgnoresIpcCableMechanical(EntityUid target, EntityUid used)
    {
        return HasComp<Content.Shared._Fish.Ipc.FishIpcComponent>(target) && _tag.HasTag(used, CableCoilTag);
    }

    private static bool IsIpcMechanical(ProtoId<DamageTypePrototype> type)
    {
        foreach (var mechanical in IpcMechanicalTypes)
        {
            if (type == mechanical)
                return true;
        }

        return false;
    }

    private static DamageSpecifier WithoutIpcMechanical(DamageSpecifier damage)
    {
        var copy = new DamageSpecifier(damage);
        foreach (var type in IpcMechanicalTypes)
        {
            copy.DamageDict.Remove(type);
        }

        return copy;
    }
    // Fish-end
}
