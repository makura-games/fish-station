using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Xenobiology.Machines;

public sealed partial class FishXenobiologyConsoleSystem
{
    partial void InitializeVerbs()
    {
        SubscribeLocalEvent<FishSlimeComponent, GetVerbsEvent<AlternativeVerb>>(OnSlimeVerbs);
        SubscribeLocalEvent<MonkeyAccentComponent, GetVerbsEvent<AlternativeVerb>>(OnMonkeyVerbs);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleVerbs);
    }

    private void OnConsoleVerbs(Entity<FishXenobiologyConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!CanOperate(ent, args.User))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-xeno-verb-release"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/out.svg.192dpi.png")),
            Act = () =>
            {
                ReleaseAllSlimes(ent, args.User);
                DirtyUi(ent, Loc.GetString("fish-xeno-console-released"));
            },
        });
    }

    private void OnSlimeVerbs(Entity<FishSlimeComponent> slime, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryGetConsoleInRange(slime, args.User, out var console))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-xeno-verb-collect"),
            Act = () =>
            {
                if (TryStoreSlime(console, slime))
                    DirtyUi(console, Loc.GetString("fish-xeno-console-collected", ("count", 1)));
            },
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-xeno-verb-scan"),
            Act = () => ScanSlime(console, slime, args.User),
        });

        if (console.Comp.LoadedPotionKind != null && console.Comp.StoredSlimes.Contains(slime))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("fish-xeno-verb-feed"),
                Act = () =>
                {
                    if (console.Comp.LoadedPotionKind is not { } kind)
                        return;

                    if (_potions.TryApplyFromConsole(slime, kind, args.User))
                    {
                        console.Comp.LoadedPotionKind = null;
                        Dirty(console);
                        DirtyUi(console, Loc.GetString("fish-xeno-console-fed"));
                    }
                },
            });
        }
    }

    private void OnMonkeyVerbs(Entity<MonkeyAccentComponent> monkey, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryGetConsoleInRange(monkey, args.User, out var console))
            return;

        if (_mobState.IsAlive(monkey))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-xeno-verb-recycle-monkey"),
            Act = () => RecycleMonkey(console, monkey, args.User),
        });
    }

    private bool TryGetConsoleInRange(EntityUid target, EntityUid user, out Entity<FishXenobiologyConsoleComponent> console)
    {
        console = default!;
        var coords = _transform.GetMapCoordinates(target);
        foreach (var uid in _lookup.GetEntitiesInRange<FishXenobiologyConsoleComponent>(coords, 8f))
        {
            if (!CanOperate(uid, user))
                continue;

            console = (uid, Comp<FishXenobiologyConsoleComponent>(uid));
            return true;
        }

        return false;
    }

    private void ScanSlime(Entity<FishXenobiologyConsoleComponent> console, EntityUid slime, EntityUid user)
    {
        if (!TryComp<FishSlimeComponent>(slime, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString("fish-slime-examine",
            ("nutrition", (int) comp.Nutrition),
            ("max", (int) comp.MaxNutrition),
            ("grown", comp.AmountGrown),
            ("need", FishSlimeMutation.EvolutionThreshold),
            ("mutation", comp.MutationChance),
            ("cores", comp.Cores),
            ("power", comp.PowerLevel),
            ("adult", comp.IsAdult),
            ("rabid", comp.Rabid),
            ("docile", comp.Docile),
            ("trained", comp.Trained)), slime, user);
    }

    private void RecycleMonkey(Entity<FishXenobiologyConsoleComponent> console, EntityUid monkey, EntityUid user)
    {
        if (_mobState.IsAlive(monkey))
            return;

        var grind = FishXenobiologyConsoleComponent.RecycleGrindRequired;
        if (console.Comp.LinkedRecycler is { } rec && TryComp<FishMonkeyRecyclerComponent>(rec, out var rc))
            grind = Math.Max(1, rc.RequiredGrind);

        QueueDel(monkey);
        console.Comp.RecycleProgress++;
        while (console.Comp.RecycleProgress >= grind)
        {
            console.Comp.RecycleProgress -= grind;
            console.Comp.MonkeyCubeCount++;
        }

        Dirty(console);
        DirtyUi(console, Loc.GetString("fish-xeno-console-recycled", ("count", 1)));
    }
}
