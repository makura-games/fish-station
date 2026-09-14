using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Implants;

public abstract class SharedImplanterSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImplanterComponent, ComponentInit>(OnImplanterInit);
        SubscribeLocalEvent<ImplanterComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<ImplanterComponent, EntRemovedFromContainerMessage>(OnEntEjected); // Sunrise-Edit
        SubscribeLocalEvent<ImplanterComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<ImplanterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ImplanterComponent, GetVerbsEvent<InteractionVerb>>(OnVerb);
        SubscribeLocalEvent<ImplanterComponent, DeimplantChangeVerbMessage>(OnSelected);
    }

    private void OnImplanterInit(EntityUid uid, ImplanterComponent component, ComponentInit args)
    {
        if (component.Implant != null)
            component.ImplanterSlot.StartingItem = component.Implant;

        _itemSlots.AddItemSlot(uid, ImplanterComponent.ImplanterSlotId, component.ImplanterSlot);

        component.DeimplantChosen ??= component.DeimplantWhitelist.FirstOrNull();

        Dirty(uid, component);
    }

    private void OnEntInserted(EntityUid uid, ImplanterComponent component, EntInsertedIntoContainerMessage args)
    {
        var implantData = Comp<MetaDataComponent>(args.Entity);
        component.ImplantData = (implantData.EntityName, implantData.EntityDescription);
        ChangeOnImplantVisualizer(uid, component); // Sunrise-Edit
        Dirty(uid, component); // Sunrise-Edit
    }

    // Sunrise-Start
    private void OnEntEjected(EntityUid uid, ImplanterComponent component, EntRemovedFromContainerMessage args)
    {
        component.ImplantData = ("", "");
        // Reset extraction mode to Random when implant is removed from extractor
        if (component.ExtractionMode != ExtractorExtractionMode.None)
            component.ExtractionMode = ExtractorExtractionMode.Random;
        ChangeOnImplantVisualizer(uid, component);
        Dirty(uid, component);
    }

    private bool CanImplantOther(EntityUid user, EntityUid target, EntityUid implanter, ImplanterComponent component)
    {
        if (component.OnlySelfImplant && target != user)
        {
            var selfMessage = Loc.GetString("implanter-only-self-implant");
            _popup.PopupEntity(selfMessage, implanter, user);
            return false;
        }

        return true;
    }
    // Sunrise-End

    private void OnExamine(EntityUid uid, ImplanterComponent component, ExaminedEvent args)
    {
        if (!component.ImplanterSlot.HasItem || !args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("implanter-contained-implant-text", ("desc", component.ImplantData.Item2)));
    }
    public bool CheckSameImplant(EntityUid target, EntityUid implant)
    {
        if (!TryComp<ImplantedComponent>(target, out var implanted))
            return false;
        var implantPrototype = Prototype(implant);
        return implanted.ImplantContainer.ContainedEntities.Any(entity => Prototype(entity) == implantPrototype);
    }

    private void OnVerb(EntityUid uid, ImplanterComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Extractor with stored implant: no verbs
        if (component.ExtractionMode != ExtractorExtractionMode.None && component.ImplanterSlot.HasItem)
            return;

        // Old-style extractor verb blocking
        if (component.BlockWhileImplantStored && component.ImplanterSlot.HasItem)
            return;

        if (component.CurrentMode == ImplanterToggleMode.Draw)
        {
            args.Verbs.Add(new InteractionVerb()
            {
                Text = Loc.GetString("implanter-set-draw-verb"),
                Act = () => TryOpenUi(uid, args.User, component)
            });
        }
    }

    private void OnUseInHand(EntityUid uid, ImplanterComponent? component, UseInHandEvent args)
    {
        if (!Resolve(uid, ref component))
            return;

        // Extractor with stored implant: completely blocked
        if (component.ExtractionMode != ExtractorExtractionMode.None && component.ImplanterSlot.HasItem)
            return;

        // Old-style extractor: block use if BlockWhileImplantStored is true
        if (component.BlockWhileImplantStored && component.ImplanterSlot.HasItem)
            return;

        if (component.CurrentMode == ImplanterToggleMode.Draw)
            TryOpenUi(uid, args.User, component);
    }

    private void OnSelected(EntityUid uid, ImplanterComponent component, DeimplantChangeVerbMessage args)
    {
        component.DeimplantChosen = args.Implant;
        SetSelectedDeimplant(uid, args.Implant, component: component);
    }

    private void TryOpenUi(EntityUid uid, EntityUid user, ImplanterComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Extractor: open radial menu when empty (regardless of current extraction mode)
        if (component.ExtractionMode != ExtractorExtractionMode.None && !component.ImplanterSlot.HasItem)
        {
            _uiSystem.TryToggleUi(uid, ExtractorRadialMenuUiKey.Key, user);
            return;
        }

        // Extractor with stored implant: block all UI
        if (component.ExtractionMode != ExtractorExtractionMode.None && component.ImplanterSlot.HasItem)
            return;

        // Old-style implanters: build filtered implant list and open dropdown
        var implantList = new Dictionary<string, string>();

        if (component.RestrictToCommonImplants)
        {
            var commonIds = new[] { "MindShieldImplant", "TrackingImplant" };
            foreach (var id in commonIds)
            {
                if (_proto.TryIndex(id, out EntityPrototype? proto))
                    implantList.Add(proto.ID, proto.Name);
            }
        }
        else
        {
            foreach (var implant in component.DeimplantWhitelist)
            {
                if (_proto.Resolve(implant, out var proto))
                    implantList.Add(proto.ID, proto.Name);
            }
        }

        if (component.AllowRandomExtraction)
            implantList.Add("__RANDOM__", Loc.GetString("implanter-random-extract"));

        _uiSystem.TryToggleUi(uid, DeimplantUiKey.Key, user);
        component.DeimplantChosen ??= component.DeimplantWhitelist.FirstOrNull();
        Dirty(uid, component);
    }

    //Instantly implant something and add all necessary components and containers.
    //Set to draw mode if not implant only
    public void Implant(EntityUid user, EntityUid target, EntityUid implanter, ImplanterComponent component)
    {
        if (!CanImplantOther(user, target, implanter, component)) // Sunrise-Edit
            return;

        // Block re-implanting an extracted implant if old-style extractor has BlockWhileImplantStored enabled
        if (component.BlockWhileImplantStored && component.ImplanterSlot.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("implanter-cannot-reimplant-extracted"), implanter, user);
            return;
        }

        // Extractor with ExtractionMode never enters Inject mode (implantOnly = true)
        if (component.ExtractionMode != ExtractorExtractionMode.None)
            return;

        if (!CanImplant(user, target, implanter, component, out var implant, out _))
            return;

        // Check if we are trying to implant a implant which is already implanted
        // Check AFTER the doafter to prevent "is it a fake?" metagaming against deceptive implants
        if (!component.AllowMultipleImplants && CheckSameImplant(target, implant.Value))
        {
            var name = Identity.Name(target, EntityManager, user);
            var msg = Loc.GetString("implanter-component-implant-already", ("implant", implant), ("target", name));
            _popup.PopupEntity(msg, target, user);
            return;
        }

        //If the target doesn't have the implanted component, add it.
        var implantedComp = EnsureComp<ImplantedComponent>(target);
        var implantContainer = implantedComp.ImplantContainer;

        if (component.ImplanterSlot.ContainerSlot != null)
            _container.Remove(implant.Value, component.ImplanterSlot.ContainerSlot);
        implantContainer.OccludesLight = false;
        _container.Insert(implant.Value, implantContainer);

        if (component.CurrentMode == ImplanterToggleMode.Inject && !component.ImplantOnly)
            DrawMode(implanter, component);
        else
            ImplantMode(implanter, component);

        var ev = new TransferDnaEvent { Donor = target, Recipient = implanter };
        RaiseLocalEvent(target, ref ev);

        Dirty(implanter, component);
    }

    public bool CanImplant(
        EntityUid user,
        EntityUid target,
        EntityUid implanter,
        ImplanterComponent component,
        [NotNullWhen(true)] out EntityUid? implant,
        [NotNullWhen(true)] out SubdermalImplantComponent? implantComp)
    {
        implant = component.ImplanterSlot.ContainerSlot?.ContainedEntities.FirstOrNull();
        if (!TryComp(implant, out implantComp))
            return false;

        if (!CheckTarget(target, component.Whitelist, component.Blacklist) ||
            !CheckTarget(target, implantComp.Whitelist, implantComp.Blacklist))
        {
            return false;
        }

        var ev = new AddImplantAttemptEvent(user, target, implant.Value, implanter);
        RaiseLocalEvent(target, ev);
        return !ev.Cancelled;
    }

    protected bool CheckTarget(EntityUid target, EntityWhitelist? whitelist, EntityWhitelist? blacklist)
    {
        return _whitelistSystem.IsWhitelistPassOrNull(whitelist, target) &&
            _whitelistSystem.IsWhitelistFailOrNull(blacklist, target);
    }

    // Draw the implant out of the target
    // TODO: Rework when surgery is in so implant cases can be a thing
    public void Draw(EntityUid implanter, EntityUid user, EntityUid target, ImplanterComponent component)
    {
        var implanterContainer = component.ImplanterSlot.ContainerSlot;

        if (implanterContainer is null)
            return;

        // EXTRACTOR-SPECIFIC: Block if implant already stored
        if (component.ImplanterSlot.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("implanter-blocked-implant-stored"), implanter, user);
            return;
        }

        // EXTRACTOR-SPECIFIC: If ExtractionMode is set, use extractor logic
        if (component.ExtractionMode != ExtractorExtractionMode.None)
        {
            DrawExtractor(implanter, user, target, component, implanterContainer);
            return;
        }

        // Original logic for other implanters
        if (!_container.TryGetContainer(target, ImplanterComponent.ImplantSlotId, out var implantContainer))
        {
            DrawCatastrophicFailure(implanter, component, user);
            return;
        }

        var implantCompQuery = GetEntityQuery<SubdermalImplantComponent>();

        // Determine which implant to extract
        EntityUid? implantToExtract = null;
        SubdermalImplantComponent? implantComp = null;

        if (component.AllowRandomExtraction && (component.DeimplantChosen == null || component.DeimplantChosen.Value.ToString() == "__RANDOM__"))
        {
            // Random mode - pick from ALL removable implants
            implantToExtract = GetRandomExtractableImplant(implantContainer, implantCompQuery);
        }
        else
        {
            // Specific implant mode - find selected implant
            implantToExtract = GetSelectedExtractableImplant(implantContainer, implantCompQuery, component);
        }

        if (implantToExtract == null || !implantCompQuery.TryGetComponent(implantToExtract.Value, out implantComp))
        {
            // FAILED EXTRACTION - apply ExtractionFailureDamage to TARGET
            _damageableSystem.TryChangeDamage(target, component.ExtractionFailureDamage, ignoreResistances: true, origin: implanter);
            _popup.PopupEntity(Loc.GetString("implanter-extraction-failed"), target, user);
            return;
        }

        // Check if permanent
        if (!_container.CanRemove(implantToExtract.Value, implantContainer))
        {
            DrawPermanentFailurePopup(implantToExtract.Value, target, user);
            return;
        }

        // SUCCESS - extract implant into implanter
        _container.Remove(implantToExtract.Value, implantContainer);
        _container.Insert(implantToExtract.Value, implanterContainer);

        // Update implant data for UI
        var implantData = Comp<MetaDataComponent>(implantToExtract.Value);
        component.ImplantData = (implantData.EntityName, implantData.EntityDescription);

        // Raise DNA transfer event
        var ev = new TransferDnaEvent { Donor = target, Recipient = implanter };
        RaiseLocalEvent(target, ref ev);

        Dirty(implanter, component);
        _popup.PopupEntity(Loc.GetString("implanter-extraction-success"), target, user);
    }

    private void DrawExtractor(EntityUid implanter, EntityUid user, EntityUid target, ImplanterComponent component, ContainerSlot implanterContainer)
    {
        if (!_container.TryGetContainer(target, ImplanterComponent.ImplantSlotId, out var implantContainer))
        {
            // No implants at all -> ExtractionFailureDamage to TARGET
            _damageableSystem.TryChangeDamage(target, component.ExtractionFailureDamage, ignoreResistances: true, origin: implanter);
            _popup.PopupEntity(Loc.GetString("implanter-extraction-failed-no-implants"), target, user);
            return;
        }

        var implantToExtract = FindImplantByExtractionMode(implantContainer, component);

        if (implantToExtract == null)
        {
            // Selected implant not found -> ExtractionFailureDamage to TARGET
            _damageableSystem.TryChangeDamage(target, component.ExtractionFailureDamage, ignoreResistances: true, origin: implanter);
            _popup.PopupEntity(Loc.GetString("implanter-extraction-failed-not-found"), target, user);
            return;
        }

        // Check if permanent (fused)
        if (!_container.CanRemove(implantToExtract.Value, implantContainer))
        {
            DrawPermanentFailurePopup(implantToExtract.Value, target, user);
            return; // NO damage for permanent implants
        }

        // SUCCESS - extract implant into implanter
        _container.Remove(implantToExtract.Value, implantContainer);
        _container.Insert(implantToExtract.Value, implanterContainer);

        // Update implant data for UI
        var implantData = Comp<MetaDataComponent>(implantToExtract.Value);
        component.ImplantData = (implantData.EntityName, implantData.EntityDescription);

        // Raise DNA transfer event
        var ev = new TransferDnaEvent { Donor = target, Recipient = implanter };
        RaiseLocalEvent(target, ref ev);

        Dirty(implanter, component);
        _popup.PopupEntity(Loc.GetString("implanter-extraction-success"), target, user);
    }

    private EntityUid? FindImplantByExtractionMode(BaseContainer implantContainer, ImplanterComponent component)
    {
        var implantCompQuery = GetEntityQuery<SubdermalImplantComponent>();
        var candidates = new List<EntityUid>();

        foreach (var entity in implantContainer.ContainedEntities)
        {
            if (!implantCompQuery.TryGetComponent(entity, out var comp))
                continue;
            if (!_container.CanRemove(entity, implantContainer))
                continue;
            candidates.Add(entity);
        }

        if (candidates.Count == 0)
            return null;

        return component.ExtractionMode switch
        {
            ExtractorExtractionMode.MindShield => FindCandidateByProtoId(candidates, "MindShieldImplant"),
            ExtractorExtractionMode.Tracking => FindCandidateByProtoId(candidates, "TrackingImplant"),
            ExtractorExtractionMode.Random => candidates[_random.Next(candidates.Count)],
            _ => null
        };
    }

    /// <summary>
    /// Find a candidate implant matching the given proto ID.
    /// Returns actual null (not struct default) when no match is found.
    /// </summary>
    private EntityUid? FindCandidateByProtoId(List<EntityUid> candidates, string targetId)
    {
        foreach (var candidate in candidates)
        {
            if (Prototype(candidate)?.ID == targetId)
                return candidate;

            if (TryComp(candidate, out SubdermalImplantComponent? comp) &&
                comp.DrawableProtoIdOverride?.ToString() == targetId)
                return candidate;
        }

        return null;
    }

    private EntityUid? GetRandomExtractableImplant(BaseContainer implantContainer, EntityQuery<SubdermalImplantComponent> implantCompQuery)
    {
        var candidates = new List<EntityUid>();
        foreach (var entity in implantContainer.ContainedEntities)
        {
            if (implantCompQuery.TryGetComponent(entity, out var comp) && _container.CanRemove(entity, implantContainer))
                candidates.Add(entity);
        }
        if (candidates.Count == 0)
            return null;
        var index = _random.Next(candidates.Count);
        return candidates[index];
    }

    private EntityUid? GetSelectedExtractableImplant(BaseContainer implantContainer, EntityQuery<SubdermalImplantComponent> implantCompQuery, ImplanterComponent component)
    {
        // Handle random selection marker
        if (component.DeimplantChosen.HasValue && component.DeimplantChosen.Value.ToString() == "__RANDOM__")
            return GetRandomExtractableImplant(implantContainer, implantCompQuery);

        foreach (var entity in implantContainer.ContainedEntities)
        {
            if (implantCompQuery.TryGetComponent(entity, out var comp))
            {
                EntProtoId? protoId = comp.DrawableProtoIdOverride;
                if (!protoId.HasValue)
                {
                    var proto = Prototype(entity);
                    if (proto != null)
                        protoId = proto.ID;
                }
                if (protoId.HasValue && component.DeimplantChosen == protoId)
                {
                    return _container.CanRemove(entity, implantContainer) ? entity : null;
                }
            }
        }
        return null;
    }

    private void DrawPermanentFailurePopup(EntityUid implant, EntityUid target, EntityUid user)
    {
        var implantName = Identity.Entity(implant, EntityManager);
        var targetName = Identity.Entity(target, EntityManager);
        var failedPermanentMessage = Loc.GetString("implanter-draw-failed-permanent",
            ("implant", implantName), ("target", targetName));
        _popup.PopupEntity(failedPermanentMessage, target, user);
    }

    private void DrawImplantIntoImplanter(EntityUid implanter, EntityUid target, EntityUid implant, BaseContainer implantContainer, ContainerSlot implanterContainer, SubdermalImplantComponent implantComp)
    {
        _container.Remove(implant, implantContainer);
        _container.Insert(implant, implanterContainer);

        var ev = new TransferDnaEvent { Donor = target, Recipient = implanter };
        RaiseLocalEvent(target, ref ev);
    }

    private void DrawCatastrophicFailure(EntityUid implanter, ImplanterComponent component, EntityUid user)
    {
        _damageableSystem.TryChangeDamage(user, component.DeimplantFailureDamage, ignoreResistances: true, origin: implanter);
        var userName = Identity.Entity(user, EntityManager);
        var failedCatastrophicallyMessage = Loc.GetString("implanter-draw-failed-catastrophically", ("user", userName));
        _popup.PopupEntity(failedCatastrophicallyMessage, user, PopupType.MediumCaution);
    }

    private void ImplantMode(EntityUid uid, ImplanterComponent component)
    {
        component.CurrentMode = ImplanterToggleMode.Inject;
        ChangeOnImplantVisualizer(uid, component);
    }

    private void DrawMode(EntityUid uid, ImplanterComponent component)
    {
        component.CurrentMode = ImplanterToggleMode.Draw;
        ChangeOnImplantVisualizer(uid, component);
    }

    private void ChangeOnImplantVisualizer(EntityUid uid, ImplanterComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        bool implantFound;

        if (component.ImplanterSlot.HasItem)
            implantFound = true;

        else
            implantFound = false;

        if (component.CurrentMode == ImplanterToggleMode.Inject && !component.ImplantOnly)
            _appearance.SetData(uid, ImplanterVisuals.Full, implantFound, appearance);

        else if (component.CurrentMode == ImplanterToggleMode.Inject && component.ImplantOnly)
        {
            _appearance.SetData(uid, ImplanterVisuals.Full, implantFound, appearance);
            _appearance.SetData(uid, ImplanterImplantOnlyVisuals.ImplantOnly, component.ImplantOnly,
                appearance);
        }

        else
            _appearance.SetData(uid, ImplanterVisuals.Full, implantFound, appearance);
    }

    public void SetSelectedDeimplant(EntityUid uid, string? implant, ImplanterComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (implant != null && _proto.TryIndex(implant, out EntityPrototype? proto))
            component.DeimplantChosen = proto;

        Dirty(uid, component);
    }
}

[Serializable, NetSerializable]
public sealed partial class ImplantEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class DrawEvent : SimpleDoAfterEvent
{
}

public sealed class AddImplantAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntityUid Target;
    public readonly EntityUid Implant;
    public readonly EntityUid Implanter;

    public AddImplantAttemptEvent(EntityUid user, EntityUid target, EntityUid implant, EntityUid implanter)
    {
        User = user;
        Target = target;
        Implant = implant;
        Implanter = implanter;
    }
}

/// <summary>
/// Change the chosen implanter in the UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeimplantChangeVerbMessage : BoundUserInterfaceMessage
{
    public readonly string? Implant;

    public DeimplantChangeVerbMessage(string? implant)
    {
        Implant = implant;
    }
}

[Serializable, NetSerializable]
public enum DeimplantUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ExtractorSetModeMessage : BoundUserInterfaceMessage
{
    public readonly ExtractorExtractionMode Mode;

    public ExtractorSetModeMessage(ExtractorExtractionMode mode)
    {
        Mode = mode;
    }
}
