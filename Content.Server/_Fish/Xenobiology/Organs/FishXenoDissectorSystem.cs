using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._Fish.Xenobiology.Organs;

/// <summary>
/// Диссектор: мёртвый моб → неопознанная масса (упрощённая таблица SS220).
/// </summary>
public sealed partial class FishXenoDissectorSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly string[] DefaultOrgans =
    [
        "FishXenoOrganRegenerativeFish",
        "FishXenoOrganToxicFish",
        "FishXenoOrganSinewFish",
        "FishXenoOrganVigorFish",
        "FishXenoOrganAntitoxinFish",
        "FishXenoOrganHeatResistFish",
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenoDissectorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FishXenoDissectorComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-dissect-alive"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var trueOrgan = PickOrgan(target);
        var mass = Spawn("FishXenoOrganMassFish", Transform(args.User).Coordinates);
        var massComp = EnsureComp<FishXenoOrganMassComponent>(mass);
        massComp.TrueOrganPrototype = trueOrgan;
        Dirty(mass, massComp);

        _popup.PopupEntity(Loc.GetString("fish-xeno-dissect-done"), args.User, args.User);
        QueueDel(target);
    }

    private string PickOrgan(EntityUid target)
    {
        var id = MetaData(target).EntityPrototype?.ID ?? "";

        if (ContainsAny(id, "Flock"))
            return "FishXenoOrganEmpPulseFish";

        if (ContainsAny(id, "Dragon"))
            return "FishXenoOrganAdrenalineFish";

        if (ContainsAny(id, "Tomato", "Clown"))
            return "FishXenoOrganAntitoxinFish";

        if (ContainsAny(id, "Watcher"))
            return "FishXenoOrganHeatResistFish";

        if (ContainsAny(id, "Spider", "Hivelord", "Hivebot", "Flesh", "Abomination"))
            return "FishXenoOrganToxicFish";

        if (ContainsAny(id, "Carp", "Goliath", "Bear", "Legion", "Megacarp"))
            return "FishXenoOrganSinewFish";

        if (ContainsAny(id, "Colossus", "Bubblegum", "Legionnaire"))
            return "FishXenoOrganSlimeSkinFish";

        if (ContainsAny(id, "Herald", "Revenant", "Morph", "Hound", "Pandora", "Goldgrub"))
            return "FishXenoOrganVigorFish";

        if (ContainsAny(id, "Monkey"))
            return _random.Pick(DefaultOrgans);

        return _random.Pick(DefaultOrgans);
    }

    private static bool ContainsAny(string id, params string[] parts)
    {
        foreach (var part in parts)
        {
            if (id.Contains(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
