using Content.Client.Implants.UI;
using Content.Client.Items;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Implants;

public sealed class ImplanterSystem : SharedImplanterSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImplanterComponent, AfterAutoHandleStateEvent>(OnHandleImplanterState);
        Subs.ItemStatus<ImplanterComponent>(ent => new ImplanterStatusControl(ent));
    }

    private void OnHandleImplanterState(EntityUid uid, ImplanterComponent component, ref AfterAutoHandleStateEvent args)
    {
        // Extractor: radial menu manages its own state; nothing else to update
        if (component.ExtractionMode != ExtractorExtractionMode.None
            || _uiSystem.TryGetOpenUi<ExtractorRadialMenuBoundUserInterface>(uid, ExtractorRadialMenuUiKey.Key, out _))
        {
            component.UiUpdateNeeded = true;
            return;
        }

        // Old-style implanters: update dropdown BUI
        if (_uiSystem.TryGetOpenUi<DeimplantBoundUserInterface>(uid, DeimplantUiKey.Key, out var deimplantBui))
        {
            Dictionary<string, string> implants = new();

            if (component.RestrictToCommonImplants)
            {
                var commonIds = new[] { "MindShieldImplant", "TrackingImplant" };
                foreach (var id in commonIds)
                {
                    if (_proto.Resolve(id, out var proto))
                        implants.Add(proto.ID, proto.Name);
                }
            }
            else
            {
                foreach (var implant in component.DeimplantWhitelist)
                {
                    if (_proto.Resolve(implant, out var proto))
                        implants.Add(proto.ID, proto.Name);
                }
            }

            if (component.AllowRandomExtraction)
                implants.Add("__RANDOM__", Loc.GetString("implanter-random-extract"));

            bool hasStoredImplant = component.ImplanterSlot.HasItem;
            bool blocked = component.BlockWhileImplantStored && hasStoredImplant;

            deimplantBui.UpdateState(implants, component.DeimplantChosen, hasStoredImplant, blocked);
        }

        component.UiUpdateNeeded = true;
    }
}
