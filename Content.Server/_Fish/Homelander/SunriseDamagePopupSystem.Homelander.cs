using Content.Shared._Fish.Homelander;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server._Sunrise.DamageOverlay;

public sealed partial class SunriseDamagePopupSystem
{
    private void ShowDamagePopup(EntityUid target, string text, EntityCoordinates coordinates,
        ICommonSession session, PopupType type)
    {
        if (TryComp<HomelanderFlightComponent>(target, out var flight) && flight.FlightEnabled)
            _popupSystem.PopupEntity(text, target, session, type);
        else
            _popupSystem.PopupCoordinates(text, coordinates, session, type);
    }
}
