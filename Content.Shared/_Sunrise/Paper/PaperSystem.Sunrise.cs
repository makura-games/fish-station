using System.Numerics;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Sunrise.Paperwork;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Paper;

public sealed partial class PaperSystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly Vector2 DefaultImageScale = new(1f, 1f);

    private void InitializeTemplateFieldSupport()
    {
        SubscribeLocalEvent<PaperComponent, PaperComponent.PaperTemplateRequestMessage>(OnTemplateRequest);
    }

    private bool TryValidateWriteTool(Entity<PaperComponent> paper, EntityUid user)
    {
        if (_hands.GetActiveItem(user) is not { } used)
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-no-write-tool"), paper, user);
            return false;
        }

        if (!_tagSystem.HasTag(used, WriteTag))
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-no-write-tool"), paper, user);
            return false;
        }

        if (paper.Comp.StampedBy.Count > 0 && !_tagSystem.HasTag(used, WriteIgnoreStampsTag))
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-stamped-write-blocked"), paper, user);
            return false;
        }

        return true;
    }

    private void OnTemplateRequest(Entity<PaperComponent> entity, ref PaperComponent.PaperTemplateRequestMessage args)
    {
        // Fish-start - подпись/должность на текущей странице пакета
        var contentTarget = ResolvePaperContentTarget(entity);
        if (!_paperQuery.TryComp(contentTarget, out var contentPaper))
            return;
        var contentEntity = new Entity<PaperComponent>(contentTarget, contentPaper);
        // Fish-end

        switch (args.Type)
        {
            case PaperComponent.PaperTemplateRequestType.Signature:
                HandleSignatureRequest(contentEntity, args.Actor, args.Index); // Fish-edit
                break;
            case PaperComponent.PaperTemplateRequestType.Job:
                HandleJobRequest(contentEntity, args.Actor, args.Index); // Fish-edit
                break;
        }

        // Fish-start - BUI открыт на внешнем листе; SetContent обновляет только внутреннюю страницу
        if (contentEntity.Owner != entity.Owner)
            RefreshUi(entity);
        // Fish-end
    }

    private bool TryValidateTemplateWriteRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!PaperInteractiveTagParsing.ContainsInteractiveTags(entity.Comp.Content))
            return false;

        if (!TryValidateWriteTool(entity, actor))
            return false;

        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(actor, ref ev);

        return !ev.Cancelled && index >= 0;
    }

    private void HandleSignatureRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!TryValidateTemplateWriteRequest(entity, actor, index))
            return;

        var signature = FormattedMessage.EscapeText(Name(actor));
        var newContent = PaperInteractiveTagParsing.ReplaceNthTag(
            entity.Comp.Content,
            PaperInteractiveTagParsing.SignatureTagRegex,
            index,
            signature);

        if (newContent == null || newContent.Length > entity.Comp.ContentSize)
            return;

        SetContent(entity, newContent);
        _audio.PlayPvs(entity.Comp.Sound, entity);
        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(actor):player} has signed {ToPrettyString(entity):entity} with: {signature}");
    }

    private void HandleJobRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!TryValidateTemplateWriteRequest(entity, actor, index))
            return;

        var jobTitle = Loc.GetString("paper-job-unknown");
        if (_idCard.TryFindIdCard(actor, out var idCard))
        {
            var title = idCard.Comp.LocalizedJobTitle;
            if (!string.IsNullOrWhiteSpace(title))
                jobTitle = title.Trim();
        }

        var jobEscaped = FormattedMessage.EscapeText(jobTitle);
        var newContent = PaperInteractiveTagParsing.ReplaceNthTag(
            entity.Comp.Content,
            PaperInteractiveTagParsing.JobTagRegex,
            index,
            jobEscaped);

        if (newContent == null || newContent.Length > entity.Comp.ContentSize)
            return;

        SetContent(entity, newContent);
        _audio.PlayPvs(entity.Comp.Sound, entity);
        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(actor):player} has filled job on {ToPrettyString(entity):entity} with: {jobEscaped}");
    }

    public void SetImageContent(Entity<PaperComponent> entity, SpriteSpecifier content, Vector2? scale = null)
    {
        entity.Comp.ImageContent = content;
        entity.Comp.ImageScale = scale ?? DefaultImageScale;
        Dirty(entity);
        UpdateUserInterface(entity);
    }

    private PaperComponent.PaperBoundUserInterfaceState GetPaperUiState(Entity<PaperComponent> entity)
    {
        // Fish-start - UI показывает текущую страницу пакета
        var contentUid = ResolvePaperContentTarget(entity);
        if (!_paperQuery.TryComp(contentUid, out var contentPaper))
            contentPaper = entity.Comp;

        var pageEv = new GetPaperPageInfoEvent(entity);
        RaiseLocalEvent(entity.Owner, ref pageEv);

        var templateFieldsEnabled = PaperInteractiveTagParsing.ContainsInteractiveTags(contentPaper.Content);

        return new PaperComponent.PaperBoundUserInterfaceState(
            contentPaper.Content,
            contentPaper.DefaultColor,
            contentPaper.StampedBy,
            entity.Comp.Mode,
            contentPaper.ImageContent,
            contentPaper.ImageScale,
            templateFieldsEnabled,
            pageEv.PageIndex,
            pageEv.PageCount,
            GetNetEntity(contentUid));
        // Fish-end
    }
}
