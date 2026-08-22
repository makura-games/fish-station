using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared._Sunrise.BluespaceArtillery;

/// <summary>
/// Режимы ЛЦУ БСА через SelectType verbs.
/// Z / UseInHand оставляем биноклю (wield + зум), без перехвата под смену режима.
/// </summary>
public abstract class SharedBluespaceArtilleryDesignatorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceArtilleryDesignatorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<BluespaceArtilleryDesignatorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<BluespaceArtilleryDesignatorComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetCurrentMode(ent, out var mode))
            return;

        args.PushMarkup(Loc.GetString("bluespace-artillery-examine-mode",
            ("mode", Loc.GetString(mode.Name))));
    }

    private void OnGetVerbs(Entity<BluespaceArtilleryDesignatorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (ent.Comp.FireModes.Count < 2)
            return;

        for (var i = 0; i < ent.Comp.FireModes.Count; i++)
        {
            var index = i;
            var mode = ent.Comp.FireModes[i];
            var modeName = Loc.GetString(mode.Name);
            var user = args.User;

            args.Verbs.Add(new Verb
            {
                Priority = 1,
                Category = VerbCategory.SelectType,
                Text = modeName,
                Disabled = i == ent.Comp.CurrentFireMode,
                Impact = LogImpact.Medium,
                DoContactInteraction = true,
                Act = () => TrySetFireMode(ent, index, user),
            });
        }
    }

    public void TryCycleFireMode(Entity<BluespaceArtilleryDesignatorComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.FireModes.Count < 2)
            return;

        var index = (ent.Comp.CurrentFireMode + 1) % ent.Comp.FireModes.Count;
        TrySetFireMode(ent, index, user);
    }

    public bool TrySetFireMode(Entity<BluespaceArtilleryDesignatorComponent> ent, int index, EntityUid? user = null)
    {
        if (index < 0 || index >= ent.Comp.FireModes.Count)
            return false;

        ent.Comp.CurrentFireMode = index;
        Dirty(ent);

        if (user != null)
        {
            var modeName = Loc.GetString(ent.Comp.FireModes[index].Name);
            _popup.PopupClient(Loc.GetString("bluespace-artillery-mode-switched", ("mode", modeName)), ent, user.Value);
        }

        return true;
    }

    public bool TryGetCurrentMode(Entity<BluespaceArtilleryDesignatorComponent> ent, out BluespaceArtilleryFireMode mode)
    {
        if (ent.Comp.CurrentFireMode < 0 || ent.Comp.CurrentFireMode >= ent.Comp.FireModes.Count)
        {
            mode = new BluespaceArtilleryFireMode();
            return false;
        }

        mode = ent.Comp.FireModes[ent.Comp.CurrentFireMode];
        return true;
    }
}
