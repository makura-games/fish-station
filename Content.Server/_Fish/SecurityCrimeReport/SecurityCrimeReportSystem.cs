using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Server._Sunrise.Messenger;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Fish.SecurityCrimeReport;
using Content.Shared._Sunrise.Laws;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emp;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Fish.SecurityCrimeReport;

/// <summary>
/// Sends curated corporate-law crime reports from the security gas mask Action radial.
/// FIsh edit
/// </summary>
public sealed class SecurityCrimeReportSystem : SharedSecurityCrimeReportSystem
{
    /// <summary>
    /// Эталон «станционных» каналов из существующего прототипа ключа — без хардкода имён подразделений.
    /// </summary>
    private static readonly EntProtoId StationMasterKeyProto = "EncryptionKeyStationMaster";

    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MessengerServerSystem _messenger = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private static readonly ProtoId<RadioChannelPrototype> SecurityChannel = "Security";
    private static readonly ProtoId<ChatNotificationPrototype> CrimeReportNotification = "SecurityCrimeReport";
    private static readonly ProtoId<ChatNotificationPrototype> CrimeReportUrgentNotification = "SecurityCrimeReportUrgent";
    private static readonly char[] InterferenceChars = new[] { '@', '#', '%', '&', '*', '$' };

    private HashSet<ProtoId<RadioChannelPrototype>> _stationChannels = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SecurityCrimeReportSelectedEvent>(OnCrimeSelected);
        CacheStationChannels();
        _prototype.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        _prototype.PrototypesReloaded -= OnPrototypesReloaded;
        base.Shutdown();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            CacheStationChannels();
    }

    private void CacheStationChannels()
    {
        _stationChannels.Clear();

        if (!_prototype.TryIndex(StationMasterKeyProto, out EntityPrototype? proto))
            return;

        if (!proto.Components.TryGetValue(_factory.GetComponentName<EncryptionKeyComponent>(), out var entry))
            return;

        if (entry.Component is not EncryptionKeyComponent key)
            return;

        _stationChannels.UnionWith(key.Channels);
    }

    private void OnCrimeSelected(SecurityCrimeReportSelectedEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } officer)
            return;

        var device = GetEntity(msg.Device);
        if (!TryComp<SecurityCrimeReportComponent>(device, out var comp))
            return;

        // Must still be wearing this mask in the mask slot.
        if (!_inventory.TryGetSlotEntity(officer, "mask", out var wornMask) || wornMask != device)
            return;

        if (!comp.Articles.Contains(msg.Law))
            return;

        if (!_prototype.TryIndex(msg.Law, out var law) ||
            string.IsNullOrEmpty(law.LawIdentifier))
            return;

        var code = law.LawIdentifier;
        var title = Loc.GetString(law.Title);
        var needsBackup = RequiresReinforcement(code);
        var malfunction = IsMalfunctioning(comp);

        string radioMessage;
        string plainMessage;

        if (malfunction)
        {
            var garbledTitle = GarbleTitle(title);
            radioMessage = Loc.GetString(needsBackup
                    ? "security-crime-report-message-malfunction-backup"
                    : "security-crime-report-message-malfunction",
                ("code", code),
                ("title", garbledTitle));
            plainMessage = Loc.GetString(needsBackup
                    ? "security-crime-report-message-malfunction-plain-backup"
                    : "security-crime-report-message-malfunction-plain",
                ("code", code),
                ("title", garbledTitle));
        }
        else
        {
            var location = FormattedMessage.RemoveMarkupOrThrow(
                _navMap.GetNearestBeaconString(officer, onlyName: true));

            if (string.IsNullOrWhiteSpace(location))
                location = Loc.GetString("nav-beacon-pos-no-beacons");

            radioMessage = Loc.GetString(needsBackup
                    ? "security-crime-report-message-backup"
                    : "security-crime-report-message",
                ("code", code),
                ("title", title),
                ("location", location));
            plainMessage = Loc.GetString(needsBackup
                    ? "security-crime-report-message-plain-backup"
                    : "security-crime-report-message-plain",
                ("code", code),
                ("title", title),
                ("location", location));
        }

        if (HasComp<EmpDisabledComponent>(device))
            return;

        if (!TryAuthorize((device, comp), officer, showPopup: true))
            return;

        var channels = CollectReportChannels(officer);
        SendSecurityAnnounce(device, officer, radioMessage, plainMessage, channels);
        NotifyChannelListeners(device, code, title, needsBackup, channels);
        StartReportCooldown(officer, comp);
    }

    /// <summary>
    /// Security всегда; плюс каналы гарнитуры, которых нет у EncryptionKeyStationMaster
    /// (CentCom / ERT / DeathSquad / BlueShield и т.п. — по данным прототипов ключей).
    /// </summary>
    private HashSet<ProtoId<RadioChannelPrototype>> CollectReportChannels(EntityUid officer)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>> { SecurityChannel };

        if (!TryGetHeadsetKeys(officer, out var keys))
            return channels;

        foreach (var channel in keys.Channels)
        {
            if (channel == SecurityChannel)
                continue;

            if (_stationChannels.Count > 0 && _stationChannels.Contains(channel))
                continue;

            channels.Add(channel);
        }

        return channels;
    }

    private bool TryGetHeadsetKeys(EntityUid officer, [NotNullWhen(true)] out EncryptionKeyHolderComponent? keys)
    {
        keys = null;

        if (TryComp<WearingHeadsetComponent>(officer, out var wearing) &&
            TryComp(wearing.Headset, out keys))
            return true;

        if (_inventory.TryGetSlotEntity(officer, "ears", out var ears) &&
            TryComp(ears, out keys))
            return true;

        return false;
    }

    private void SendSecurityAnnounce(
        EntityUid device,
        EntityUid officer,
        string radioMessage,
        string plainMessage,
        HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        var station = _station.GetOwningStation(device) ?? _station.GetOwningStation(officer);

        if (_messenger.GetServerEntity(station) is var (server, _))
        {
            foreach (var channel in channels)
            {
                if (_messenger.GetGroupIdByRadioChannel(channel.Id) is { } groupId)
                    _messenger.SendSystemMessageToGroup(server, groupId, plainMessage);
            }
        }

        // Последовательные вызовы: _messages в RadioSystem — защита от реэнтрантности внутри одного send,
        // к концу метода сообщение снимается, поэтому второй канал с тем же текстом проходит штатно.
        foreach (var channel in channels)
        {
            _radio.SendRadioMessage(officer, radioMessage, channel, device, escapeMarkup: false);
        }
    }

    /// <summary>
    /// Звук + Notifications для слушателей любого из каналов доклада.
    /// </summary>
    private void NotifyChannelListeners(
        EntityUid source,
        string code,
        string title,
        bool urgent,
        HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        var proto = urgent ? CrimeReportUrgentNotification : CrimeReportNotification;
        var summary = $"{code} — {title}";
        var notified = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var radio, out var xform))
        {
            if (!radio.ReceiveAllChannels && !ChannelsOverlap(radio.Channels, channels))
                continue;

            if (TryComp<HeadsetComponent>(receiver, out var headset) &&
                !HasEnabledChannel(headset, channels))
                continue;

            var target = receiver;
            if (!HasComp<ActorComponent>(receiver))
            {
                var parent = xform.ParentUid;
                if (!parent.IsValid() || !HasComp<ActorComponent>(parent))
                    continue;

                target = parent;
            }

            if (!notified.Add(target))
                continue;

            var ev = new ChatNotificationEvent(proto, source)
            {
                SourceNameOverride = summary,
            };
            RaiseLocalEvent(target, ref ev);
        }
    }

    private static bool ChannelsOverlap(
        HashSet<ProtoId<RadioChannelPrototype>> radioChannels,
        HashSet<ProtoId<RadioChannelPrototype>> reportChannels)
    {
        foreach (var channel in reportChannels)
        {
            if (radioChannels.Contains(channel))
                return true;
        }

        return false;
    }

    private static bool HasEnabledChannel(
        HeadsetComponent headset,
        HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        foreach (var channel in channels)
        {
            if (headset.EnabledChannels.GetValueOrDefault(channel, true))
                return true;
        }

        return false;
    }

    private string GarbleTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return Loc.GetString("security-crime-report-interference");

        var result = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (char.IsLetter(c) && _random.Prob(0.35f))
                result.Append(_random.Pick(InterferenceChars));
            else
                result.Append(c);
        }

        return result.ToString();
    }
}
