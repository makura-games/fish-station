using Content.Shared.Chemistry.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimeExtractSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSlimeExtractComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FishSlimeExtractComponent, InteractUsingEvent>(OnInteractUsing);

        InitializeReactions();
    }

    partial void InitializeReactions();

    public bool TryAddUse(Entity<FishSlimeExtractComponent> extract, EntityUid? user)
    {
        if (!CanAddUse(extract, user))
            return false;

        var wasSpent = extract.Comp.Uses <= 0;
        extract.Comp.Uses++;
        Dirty(extract);

        if (wasSpent)
            RestoreName(extract);

        if (user is { } userUid)
            _popup.PopupEntity(Loc.GetString("fish-slime-extract-enhanced"), extract, userUid);

        return true;
    }

    public bool CanAddUse(Entity<FishSlimeExtractComponent> extract, EntityUid? user, bool quiet = false)
    {
        if (extract.Comp.Uses < extract.Comp.MaxUses)
            return true;

        if (!quiet && user is { } userUid)
            _popup.PopupEntity(Loc.GetString("fish-slime-extract-enhance-full"), extract, userUid);

        return false;
    }

    public void ConsumeUse(Entity<FishSlimeExtractComponent> extract)
    {
        if (extract.Comp.Uses <= 0)
            return;

        extract.Comp.Uses--;
        Dirty(extract);

        if (extract.Comp.Uses > 0)
            return;

        _meta.SetEntityName(extract, Loc.GetString("fish-slime-extract-used-name"));
        _meta.SetEntityDescription(extract, Loc.GetString("fish-slime-extract-used-desc"));
    }

    private void OnExamined(Entity<FishSlimeExtractComponent> ent, ref ExaminedEvent args)
    {
        args.PushText(Loc.GetString("fish-slime-extract-examine-uses", ("uses", ent.Comp.Uses), ("max", ent.Comp.MaxUses)));
    }

    private void OnInteractUsing(Entity<FishSlimeExtractComponent> ent, ref InteractUsingEvent args)
    {
        // Шприц запоминает того, кто колол ядро, ещё до самой реакции.
        if (!HasComp<InjectorComponent>(args.Used))
            return;

        ent.Comp.LastInjector = args.User;
    }

    private void RestoreName(Entity<FishSlimeExtractComponent> extract)
    {
        if (MetaData(extract).EntityPrototype is not { } proto)
            return;

        _meta.SetEntityName(extract, proto.Name);
        _meta.SetEntityDescription(extract, proto.Description);
    }
}
