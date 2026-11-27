using Content.Client.UserInterface.Controls;
using Content.Shared._Fish.SecurityCrimeReport;
using Content.Shared._Sunrise.Laws;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Fish.SecurityCrimeReport;

/// <summary>
/// Opens SimpleRadialMenu from the mask InstantAction, then sends the selected article to the server.
/// FIsh edit
/// </summary>
public sealed class SecurityCrimeReportSystem : SharedSecurityCrimeReportSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private SimpleRadialMenu? _menu;

    /// <summary>
    /// SS13 HUD icons (tgstation hud.dmi → Interface/Misc/security_icons.rsi).
    /// </summary>
    private static readonly ResPath SecurityHudIcons = new("Interface/Misc/security_icons.rsi");

    public override void Shutdown()
    {
        CloseMenu();
        base.Shutdown();
    }

    protected override void OnOpenAuthorized(Entity<SecurityCrimeReportComponent> ent, ref OpenSecurityCrimeReportEvent args)
    {
        var local = _player.LocalEntity;
        if (local == null || args.Performer != local.Value)
            return;

        OpenRadial(ent);
    }

    private void OpenRadial(Entity<SecurityCrimeReportComponent> ent)
    {
        CloseMenu();

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var lawId in ent.Comp.Articles)
        {
            if (!_prototype.TryIndex(lawId, out var law) || string.IsNullOrEmpty(law.LawIdentifier))
                continue;

            var title = Loc.GetString(law.Title);
            var tooltip = Loc.GetString("security-crime-report-menu-article",
                ("code", law.LawIdentifier),
                ("title", title));

            var (iconState, color) = GetArticleVisuals(law.LawIdentifier);

            buttons.Add(new RadialMenuActionOption<ProtoId<CorporateLawPrototype>>(id => OnSelected(ent.Owner, id), lawId)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(SecurityHudIcons, iconState)),
                ToolTip = tooltip,
                BackgroundColor = color,
            });
        }

        if (buttons.Count == 0)
            return;

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(buttons);
        _menu.OpenCentered();
        _menu.OnClose += CloseMenu;
    }

    /// <summary>
    /// Maps Fish Space Law severity to SS13 SecHUD status icons + tint.
    /// </summary>
    private static (string State, Color Background) GetArticleVisuals(string code)
    {
        // Special high-priority articles first.
        switch (code)
        {
            case "312": // violence against authority
                return ("hud_wanted", Color.FromHex("#8B0000AA"));
            case "304": // contraband
                return ("hud_suspected", Color.FromHex("#DAA520AA"));
            case "401": // murder
                return ("hud_hostile", Color.FromHex("#FF0000AA"));
            case "502": // terror attack
                return ("hud_eliminated", Color.FromHex("#4B0082AA"));
        }

        if (code.Length == 0)
            return ("hud_wanted", Color.FromHex("#FF4242AA"));

        return code[0] switch
        {
            '1' => ("hud_suspected", Color.FromHex("#F0E68CAA")), // 1xx minor
            '2' => ("hud_paroled", Color.FromHex("#FFA500AA")), // 2xx light
            '3' => ("hud_wanted", Color.FromHex("#FF4242AA")), // 3xx felony
            '4' => ("hud_hostile", Color.FromHex("#DC143CAA")), // 4xx grand
            '5' => ("hud_eliminated", Color.FromHex("#800080AA")), // 5xx capital
            _ => ("hud_wanted", Color.FromHex("#FF4242AA")),
        };
    }

    private void OnSelected(EntityUid device, ProtoId<CorporateLawPrototype> law)
    {
        RaiseNetworkEvent(new SecurityCrimeReportSelectedEvent(GetNetEntity(device), law));
        CloseMenu();
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        _menu.OnClose -= CloseMenu;
        _menu.Dispose();
        _menu = null;
    }
}
