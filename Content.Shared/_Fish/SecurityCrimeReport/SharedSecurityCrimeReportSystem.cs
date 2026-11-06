using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Emp;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.SecurityCrimeReport;

/// <summary>
/// Shared Action Hotbar grant/revoke, access checks, EMP malfunction and report cooldown for the security gas mask.
/// FIsh edit
/// </summary>
public abstract class SharedSecurityCrimeReportSystem : EntitySystem
{
    public const string ReportDelayId = "security-crime-report";

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityCrimeReportComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SecurityCrimeReportComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SecurityCrimeReportComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<SecurityCrimeReportComponent, OpenSecurityCrimeReportEvent>(OnOpenAction);
        SubscribeLocalEvent<SecurityCrimeReportComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnMapInit(Entity<SecurityCrimeReportComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<SecurityCrimeReportComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnGetItemActions(Entity<SecurityCrimeReportComponent> ent, ref GetItemActionsEvent args)
    {
        // Only while worn in the clothing slot (mask), same pattern as AddWantedStatus / item actions.
        if (!TryComp<ClothingComponent>(ent.Owner, out var clothing))
            return;

        if (clothing.Slots != args.SlotFlags)
            return;

        // Без Security Access action не показываем (стандартный AccessReader на маске).
        if (!_access.IsAllowed(args.User, ent.Owner))
            return;

        args.AddAction(ent.Comp.ActionEntity);
        SyncActionCooldownFromUser(args.User, ent.Comp);
    }

    private void OnOpenAction(Entity<SecurityCrimeReportComponent> ent, ref OpenSecurityCrimeReportEvent args)
    {
        HandleOpenAction(ent, ref args);
    }

    /// <summary>
    /// Проверяет доступ и cooldown; при успехе помечает InstantAction handled и вызывает <see cref="OnOpenAuthorized"/>.
    /// </summary>
    protected virtual void HandleOpenAction(Entity<SecurityCrimeReportComponent> ent, ref OpenSecurityCrimeReportEvent args)
    {
        if (args.Handled)
            return;

        if (!TryAuthorize(ent, args.Performer, showPopup: true))
        {
            // Съедаем клик без старта useDelay (у Action нет useDelay — cooldown только после доклада).
            args.Handled = true;
            return;
        }

        args.Handled = true;
        OnOpenAuthorized(ent, ref args);
    }

    /// <summary>
    /// Client opens SimpleRadialMenu; server has nothing extra to do on open.
    /// </summary>
    protected virtual void OnOpenAuthorized(Entity<SecurityCrimeReportComponent> ent, ref OpenSecurityCrimeReportEvent args)
    {
    }

    private void OnEmpPulse(Entity<SecurityCrimeReportComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        ent.Comp.MalfunctionUntil = Timing.CurTime + ent.Comp.MalfunctionDuration;
        Dirty(ent);
    }

    /// <summary>
    /// AccessReader + UseDelay на носителе (общий для всех масок).
    /// </summary>
    protected bool TryAuthorize(Entity<SecurityCrimeReportComponent> ent, EntityUid user, bool showPopup)
    {
        if (!_access.IsAllowed(user, ent.Owner))
        {
            if (showPopup)
                _popup.PopupClient(Loc.GetString("lock-comp-has-user-access-fail"), ent.Owner, user);

            return false;
        }

        if (_useDelay.IsDelayed(user, ReportDelayId))
            return false;

        return true;
    }

    /// <summary>
    /// Ставит UseDelay на носителя и cooldown на Action текущей маски.
    /// </summary>
    protected void StartReportCooldown(EntityUid user, SecurityCrimeReportComponent comp)
    {
        _useDelay.SetLength(user, comp.ReportCooldown, ReportDelayId);
        _useDelay.TryResetDelay(user, id: ReportDelayId);

        if (comp.ActionEntity is { } action)
            _actions.SetCooldown(action, comp.ReportCooldown);
    }

    /// <summary>
    /// Подтягивает оставшийся UseDelay носителя на ActionEntity маски (вторая маска / переэкипировка).
    /// </summary>
    protected void SyncActionCooldownFromUser(EntityUid user, SecurityCrimeReportComponent comp)
    {
        if (comp.ActionEntity is not { } action)
            return;

        if (!_useDelay.TryGetDelayInfo(user, out var info, ReportDelayId))
            return;

        if (info.EndTime <= Timing.CurTime)
            return;

        _actions.SetCooldown(action, info.StartTime, info.EndTime);
    }

    protected bool IsMalfunctioning(SecurityCrimeReportComponent comp)
    {
        return comp.MalfunctionUntil is { } until && Timing.CurTime < until;
    }

    /// <summary>
    /// Тяжесть берётся из кода статьи Fish Space Law (1xx–5xx), без дублирования данных в прототипе.
    /// 4xx/5xx — всегда; отдельные 3xx — насилие над властью, тяжкий ущерб, беспорядки, похищение.
    /// </summary>
    protected static bool RequiresReinforcement(string lawIdentifier)
    {
        if (string.IsNullOrEmpty(lawIdentifier))
            return false;

        // Wiki: 4XX особо тяжкие, 5XX критические.
        if (lawIdentifier[0] is '4' or '5')
            return true;

        // Тяжкие 3XX, где по процедуре СБ нужен бэкап.
        return lawIdentifier is "300" or "301" or "310" or "312";
    }
}
