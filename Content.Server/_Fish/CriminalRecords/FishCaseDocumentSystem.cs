using System.Text;
using System.Text.RegularExpressions;
using Content.Server._Sunrise.CriminalRecords.Components;
using Content.Server._Sunrise.Documents;
using Content.Shared._Fish.CriminalRecords;
using Content.Shared._Sunrise.CopyMachine;
using Content.Shared._Sunrise.CriminalRecords;
using Content.Shared._Sunrise.CriminalRecords.Components;
using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.Laws.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.CriminalRecords;

/// <summary>
/// Быстрая печать приговора/ордера с консоли уголовных дел по данным дела.
/// </summary>
public sealed partial class FishCaseDocumentSystem : EntitySystem
{
    private static readonly Regex DocumentTagRegex =
        new("<Document>(.*?)</Document>", RegexOptions.Singleline | RegexOptions.Compiled);

    [Dependency] private DocumentFormatSystem _documentFormat = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StationRecordsSystem _stationRecords = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedStationCorporateLawSystem _corporateLaw = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SunriseCriminalRecordsConsoleComponent>(SunriseCriminalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<FishCriminalRecordsPrintDocumentMessage>(OnPrint);
        });
    }

    private void OnPrint(Entity<SunriseCriminalRecordsConsoleComponent> ent, ref FishCriminalRecordsPrintDocumentMessage args)
    {
        if (!CheckAccess(ent, args.Actor))
            return;

        if (ent.Comp.SelectedKey is not { } key)
            return;

        // Печать только для той записи, которую клиент имел в виду при запросе.
        if (key.Id != args.RecordId)
            return;

        if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var general))
            return;

        if (!TryComp<StationCriminalRecordsComponent>(key.OriginStation, out var stationRecords))
            return;

        if (!stationRecords.Records.TryGetValue(key.Id, out var personCases))
            return;

        CriminalCase? criminalCase = null;
        foreach (var c in personCases)
        {
            if (c.Id != args.CaseId)
                continue;
            criminalCase = c;
            break;
        }

        if (criminalCase == null)
            return;

        if (args.TemplateId is not (FishQuickDocumentTemplates.Sentence or FishQuickDocumentTemplates.Warrant))
            return;

        if (!TryLoadTemplate(args.TemplateId, out var content))
            return;

        content = _documentFormat.Format(content, ent);
        content = ApplyCasePlaceholders(content, ent, general, criminalCase);

        var paperUid = Spawn("Paper", Transform(ent).Coordinates);
        if (!TryComp<PaperComponent>(paperUid, out var paperComp))
        {
            Del(paperUid);
            return;
        }

        _paper.SetContent((paperUid, paperComp), content);

        if (_prototypes.TryIndex<DocTemplatePrototype>(args.TemplateId, out var template) && template.Header != null)
            _paper.SetImageContent((paperUid, paperComp), template.Header);

        _hands.PickupOrDrop(args.Actor, paperUid);
    }

    private bool CheckAccess(EntityUid console, EntityUid user)
    {
        return _accessReader.IsAllowed(user, console);
    }

    private bool TryLoadTemplate(string templateId, out string content)
    {
        content = string.Empty;
        if (!_prototypes.TryIndex<DocTemplatePrototype>(templateId, out var template))
            return false;

        using var file = _resources.ContentFileReadText(template.Content);
        var fileText = file.ReadToEnd();
        var match = DocumentTagRegex.Match(fileText);
        content = match.Success ? match.Groups[1].Value.Trim() : fileText.Trim();
        return !string.IsNullOrWhiteSpace(content);
    }

    private string ApplyCasePlaceholders(
        string content,
        EntityUid console,
        GeneralStationRecord general,
        CriminalCase criminalCase)
    {
        var prisonerName = general.Name ?? string.Empty;
        var prisonerJob = general.JobTitle ?? string.Empty;
        var caseId = criminalCase.Id.ToString();
        var notes = criminalCase.Notes ?? string.Empty;

        var laws = FormatLaws(criminalCase.Laws);
        var circumstances = FormatLaws(criminalCase.Circumstances);
        var sentence = FormatSentence(console, criminalCase);

        var incarcerationStart = criminalCase.IncarcerationStartTime is { } start
            ? $"{start:hh\\:mm}"
            : string.Empty;

        // Пустые значения не подставляем выдуманные данные — оставляем пустую строку.
        return content
            .Replace("{prisonerName}", prisonerName)
            .Replace("{prisonerJob}", prisonerJob)
            .Replace("{caseId}", caseId)
            .Replace("{lawsList}", laws)
            .Replace("{circumstancesList}", circumstances)
            .Replace("{sentenceSummary}", sentence)
            .Replace("{caseNotes}", notes)
            .Replace("{incarcerationStart}", incarcerationStart);
    }

    private string FormatLaws(List<ProtoId<CorporateLawPrototype>> laws)
    {
        if (laws.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var index = 1;
        foreach (var lawId in laws)
        {
            if (!_prototypes.TryIndex(lawId, out var law))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');

            var identifier = law.LawIdentifier ?? law.ID;
            sb.Append($"{index}) {identifier} — {Loc.GetString(law.Title)}");
            index++;
        }

        return sb.ToString();
    }

    private string FormatSentence(EntityUid console, CriminalCase criminalCase)
    {
        if (criminalCase.IsWarning)
            return Loc.GetString("fish-criminal-doc-sentence-warning");

        if (criminalCase.CalculatedSentence <= 0)
            return string.Empty;

        _corporateLaw.GetEffectiveLawset(console, out _, out _, out _, out var threshold);
        if (criminalCase.CalculatedSentence >= threshold)
            return Loc.GetString("sunrise-records-sentence-life");

        return Loc.GetString("fish-criminal-doc-sentence-minutes", ("minutes", criminalCase.CalculatedSentence));
    }
}
