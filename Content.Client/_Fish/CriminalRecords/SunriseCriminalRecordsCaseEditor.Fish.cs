using System.Numerics;
using System.Text;
using Content.Shared._Fish.CriminalRecords;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Sunrise.CriminalRecords.UI;

public sealed partial class SunriseCriminalRecordsCaseEditor
{
    public Action<uint, uint, string>? OnPrintQuickDocument;
    public string? PrisonerName;
    public string? PrisonerJob;
    public uint? SelectedStationRecord;

    private int _previewSentence;

    private void WireQuickPrint()
    {
        PrintSentenceButton.OnPressed += _ => TryPrintQuick(FishQuickDocumentTemplates.Sentence);
        PrintWarrantButton.OnPressed += _ => TryPrintQuick(FishQuickDocumentTemplates.Warrant);
    }

    private void SetQuickPrintEnabled(bool enabled)
    {
        PrintSentenceButton.Disabled = !enabled;
        PrintWarrantButton.Disabled = !enabled;
    }

    private void CacheQuickPrintSentence(int total, bool isWarning)
    {
        _previewSentence = total;
        _isWarning = isWarning;
    }

    private void TryPrintQuick(string templateId)
    {
        if (_lastCaseId is not { } caseId || SelectedStationRecord is not { } recordId)
            return;

        // Сохраняем заметки до печати — иначе сервер получит старое значение.
        if (_dirty)
            Save();

        UpdateSentenceDisplay();

        var name = string.IsNullOrWhiteSpace(PrisonerName)
            ? Loc.GetString("fish-criminal-records-preview-empty")
            : PrisonerName;
        var job = string.IsNullOrWhiteSpace(PrisonerJob)
            ? Loc.GetString("fish-criminal-records-preview-empty")
            : PrisonerJob;

        _stationLaw.GetEffectiveLawset(ConsoleEntity, out _, out _, out _, out var threshold);
        string sentence;
        if (_isWarning)
            sentence = Loc.GetString("fish-criminal-doc-sentence-warning");
        else if (_previewSentence >= threshold)
            sentence = Loc.GetString("sunrise-records-sentence-life");
        else if (_previewSentence > 0)
            sentence = Loc.GetString("fish-criminal-doc-sentence-minutes", ("minutes", _previewSentence));
        else
            sentence = Loc.GetString("fish-criminal-records-preview-empty");

        var lawsSb = new StringBuilder();
        foreach (var lawId in _currentLaws)
        {
            if (!_proto.TryIndex(lawId, out var law))
                continue;
            if (lawsSb.Length > 0)
                lawsSb.Append(", ");
            lawsSb.Append(law.LawIdentifier ?? law.ID);
        }

        var preview = Loc.GetString("fish-criminal-records-preview-prisoner", ("name", name), ("job", job))
                      + "\n"
                      + Loc.GetString("fish-criminal-records-preview-case", ("id", caseId))
                      + "\n"
                      + Loc.GetString("fish-criminal-records-preview-sentence", ("sentence", sentence))
                      + "\n"
                      + Loc.GetString("fish-criminal-records-preview-laws")
                      + " "
                      + (lawsSb.Length > 0 ? lawsSb.ToString() : Loc.GetString("fish-criminal-records-preview-empty"));

        var dialog = new DefaultWindow
        {
            Title = Loc.GetString("fish-criminal-records-print-confirm-title"),
            MinSize = new Vector2(360, 200)
        };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        box.AddChild(new Label { Text = preview });
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var confirm = new Button { Text = Loc.GetString("fish-criminal-records-print-confirm") };
        var cancel = new Button { Text = Loc.GetString("fish-criminal-records-print-cancel") };
        confirm.OnPressed += _ =>
        {
            OnPrintQuickDocument?.Invoke(recordId, caseId, templateId);
            dialog.Close();
        };
        cancel.OnPressed += _ => dialog.Close();
        buttons.AddChild(confirm);
        buttons.AddChild(cancel);
        box.AddChild(buttons);
        dialog.AddChild(box);
        dialog.OpenCentered();
    }
}
