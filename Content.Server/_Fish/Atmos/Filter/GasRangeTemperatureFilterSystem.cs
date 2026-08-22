using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Server.Administration.Logs;
using Content.Server.NodeContainer.EntitySystems;

namespace Content.Server.Atmos.Piping.Trinary.EntitySystems;

[UsedImplicitly]
public sealed class GasRangeTemperatureFilterSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;

    // Максимальная разумная температура (можно взять из Atmospherics, если есть)
    private const float MaxAllowedTemperature = 100000f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, AtmosDeviceUpdateEvent>(OnFilterUpdated);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, AtmosDeviceDisabledEvent>(OnFilterLeaveAtmosphere);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, ActivateInWorldEvent>(OnFilterActivate);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, GasAnalyzerScanEvent>(OnFilterAnalyzed);

        // UI сообщения
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, GasRangeTemperatureFilterToggleStatusMessage>(OnToggleStatus);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, GasRangeTemperatureFilterChangeRateMessage>(OnChangeRate);
        SubscribeLocalEvent<GasRangeTemperatureFilterComponent, GasRangeTemperatureFilterChangeRangeMessage>(OnChangeRange);
    }

    private void OnInit(EntityUid uid, GasRangeTemperatureFilterComponent filter, ComponentInit args)
    {
        // Приводим диапазон к корректному виду
        NormalizeRange(filter);
        UpdateAppearance(uid, filter);
    }

    private void OnFilterUpdated(EntityUid uid, GasRangeTemperatureFilterComponent filter, ref AtmosDeviceUpdateEvent args)
    {
        if (!filter.Enabled)
        {
            _ambientSound.SetAmbience(uid, false);
            return;
        }

        if (!_nodeContainer.TryGetNodes(uid, filter.InletName, filter.BypassName, filter.FilteredName,
                out PipeNode? inlet, out PipeNode? bypass, out PipeNode? filtered))
        {
            _ambientSound.SetAmbience(uid, false);
            return;
        }

        // Если оба выхода переполнены – ничего не делаем
        if (bypass.Air.Pressure >= Atmospherics.MaxOutputPressure || filtered.Air.Pressure >= Atmospherics.MaxOutputPressure)
        {
            _ambientSound.SetAmbience(uid, false);
            return;
        }

        var transferVol = filter.TransferRate * _atmosphere.PumpSpeedup() * args.dt;
        if (transferVol <= 0)
        {
            _ambientSound.SetAmbience(uid, false);
            return;
        }

        var removed = inlet.Air.RemoveVolume(transferVol);
        if (removed.TotalMoles == 0)
        {
            _ambientSound.SetAmbience(uid, false);
            return;
        }

        // Проверяем, попадает ли температура смеси в диапазон
        bool inRange = removed.Temperature >= filter.LowTemperature && removed.Temperature <= filter.HighTemperature;

        var targetNode = inRange ? filtered : bypass;

        // Если целевой узел переполнен – пытаемся направить в другой, если он не переполнен
        if (targetNode.Air.Pressure >= Atmospherics.MaxOutputPressure)
        {
            targetNode = (targetNode == filtered) ? bypass : filtered;
            if (targetNode.Air.Pressure >= Atmospherics.MaxOutputPressure)
            {
                // оба переполнены – возвращаем газ обратно во вход
                _atmosphere.Merge(inlet.Air, removed);
                _ambientSound.SetAmbience(uid, false);
                return;
            }
        }

        _atmosphere.Merge(targetNode.Air, removed);
        _ambientSound.SetAmbience(uid, removed.TotalMoles > 0f);
    }

    private void OnFilterLeaveAtmosphere(EntityUid uid, GasRangeTemperatureFilterComponent filter, ref AtmosDeviceDisabledEvent args)
    {
        filter.Enabled = false;
        UpdateAppearance(uid, filter);
        _ambientSound.SetAmbience(uid, false);
        DirtyUI(uid, filter);
        _userInterface.CloseUi(uid, GasRangeTemperatureFilterUiKey.Key);
    }

    private void OnFilterActivate(EntityUid uid, GasRangeTemperatureFilterComponent filter, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (Comp<TransformComponent>(uid).Anchored)
        {
            _userInterface.OpenUi(uid, GasRangeTemperatureFilterUiKey.Key, actor.PlayerSession);
            DirtyUI(uid, filter);
        }
        else
        {
            _popup.PopupCursor(Loc.GetString("comp-gas-range-temp-filter-ui-needs-anchor"), args.User);
        }

        args.Handled = true;
    }

    private void DirtyUI(EntityUid uid, GasRangeTemperatureFilterComponent? filter)
    {
        if (!Resolve(uid, ref filter))
            return;

        _userInterface.SetUiState(uid, GasRangeTemperatureFilterUiKey.Key,
            new GasRangeTemperatureFilterBoundUserInterfaceState(
                MetaData(uid).EntityName,
                filter.TransferRate,
                filter.Enabled,
                filter.LowTemperature,
                filter.HighTemperature));
    }

    private void UpdateAppearance(EntityUid uid, GasRangeTemperatureFilterComponent? filter = null)
    {
        if (!Resolve(uid, ref filter, false))
            return;

        _appearance.SetData(uid, FilterVisuals.Enabled, filter.Enabled);
    }

    // --- Обработчики UI сообщений ---

    private void OnToggleStatus(EntityUid uid, GasRangeTemperatureFilterComponent filter, GasRangeTemperatureFilterToggleStatusMessage args)
    {
        filter.Enabled = args.Enabled;
        _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(uid):device} to {args.Enabled}");
        DirtyUI(uid, filter);
        UpdateAppearance(uid, filter);
    }

    private void OnChangeRate(EntityUid uid, GasRangeTemperatureFilterComponent filter, GasRangeTemperatureFilterChangeRateMessage args)
    {
        filter.TransferRate = Math.Clamp(args.Rate, 0f, filter.MaxTransferRate);
        _adminLogger.Add(LogType.AtmosVolumeChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the transfer rate on {ToPrettyString(uid):device} to {args.Rate}");
        DirtyUI(uid, filter);
    }

    private void OnChangeRange(EntityUid uid, GasRangeTemperatureFilterComponent filter, GasRangeTemperatureFilterChangeRangeMessage args)
    {
        // Нормализуем и ограничиваем значения
        float low = Math.Max(Atmospherics.TCMB, args.Low);
        float high = Math.Min(MaxAllowedTemperature, args.High);
        if (low > high)
        {
            // Меняем местами
            (low, high) = (high, low);
        }
        filter.LowTemperature = low;
        filter.HighTemperature = high;

        _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set temperature range on {ToPrettyString(uid):device} to [{low}, {high}] K");
        DirtyUI(uid, filter);
    }

    // --- Анализатор газа ---

    private void OnFilterAnalyzed(EntityUid uid, GasRangeTemperatureFilterComponent component, GasAnalyzerScanEvent args)
    {
        args.GasMixtures ??= new List<(string, GasMixture?)>();

        if (_nodeContainer.TryGetNode(uid, component.InletName, out PipeNode? inlet) && inlet.Air.Volume != 0f)
        {
            var inletAirLocal = inlet.Air.Clone();
            inletAirLocal.Multiply(inlet.Volume / inlet.Air.Volume);
            inletAirLocal.Volume = inlet.Volume;
            args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-inlet"), inletAirLocal));
        }
        if (_nodeContainer.TryGetNode(uid, component.BypassName, out PipeNode? bypass) && bypass.Air.Volume != 0f)
        {
            var bypassAirLocal = bypass.Air.Clone();
            bypassAirLocal.Multiply(bypass.Volume / bypass.Air.Volume);
            bypassAirLocal.Volume = bypass.Volume;
            args.GasMixtures.Add((Loc.GetString("comp-gas-range-temp-filter-bypass"), bypassAirLocal));
        }
        if (_nodeContainer.TryGetNode(uid, component.FilteredName, out PipeNode? filtered) && filtered.Air.Volume != 0f)
        {
            var filteredAirLocal = filtered.Air.Clone();
            filteredAirLocal.Multiply(filtered.Volume / filtered.Air.Volume);
            filteredAirLocal.Volume = filtered.Volume;
            args.GasMixtures.Add((Loc.GetString("comp-gas-range-temp-filter-filtered"), filteredAirLocal));
        }

        // Определяем, перевёрнуто ли устройство – по направлениям труб, упрощённо false
        args.DeviceFlipped = false;
    }

    private void NormalizeRange(GasRangeTemperatureFilterComponent filter)
    {
        if (filter.LowTemperature < Atmospherics.TCMB)
            filter.LowTemperature = Atmospherics.TCMB;
        if (filter.HighTemperature > MaxAllowedTemperature)
            filter.HighTemperature = MaxAllowedTemperature;
        if (filter.LowTemperature > filter.HighTemperature)
            (filter.LowTemperature, filter.HighTemperature) = (filter.HighTemperature, filter.LowTemperature);
    }
}