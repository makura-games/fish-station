using Content.Client.UserInterface.Controls;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Implants.UI;

public sealed class ExtractorRadialMenuBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;
    private readonly IPrototypeManager _proto = IoCManager.Resolve<IPrototypeManager>();

    public ExtractorRadialMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        var options = new List<RadialMenuOptionBase>
        {
            CreateOption(ExtractorExtractionMode.MindShield, "MindShieldImplant", "implanter-radial-mindshield"),
            CreateOption(ExtractorExtractionMode.Tracking, "TrackingImplant", "implanter-radial-tracking"),
            CreateOption(ExtractorExtractionMode.Random, null, "implanter-radial-random")
        };

        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
    }

    private RadialMenuActionOption<ExtractorExtractionMode> CreateOption(
        ExtractorExtractionMode mode, string? prototypeId, string locKey)
    {
        var icon = prototypeId != null
            ? RadialMenuIconSpecifier.With((EntProtoId)prototypeId)
            : RadialMenuIconSpecifier.With((EntProtoId)"d6Dice");

        return new RadialMenuActionOption<ExtractorExtractionMode>(OnOptionSelected, mode)
        {
            IconSpecifier = icon,
            ToolTip = Loc.GetString(locKey)
        };
    }

    private void OnOptionSelected(ExtractorExtractionMode mode)
    {
        SendMessage(new ExtractorSetModeMessage(mode));
        Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state) { }
}