using Content.Client._Starlight;
using Content.Client._Fish.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Medical.Surgery;

public sealed partial class SurgeryBui
{
    /* Серверные снимки обновляют содержимое текущего раздела, не выполняя навигацию заново. */
    private readonly List<(Entity<OrganComponent> Part, string Name)> _fishParts = new();
    private readonly List<(EntProtoId Id, string Suffix, bool Completed)> _fishShownChoices = new();
    private readonly Dictionary<EntProtoId, ChoiceControl> _fishOperationButtons = new();
    private readonly Dictionary<SurgeryStepButton, FishStepPresentation> _fishStepPresentations = new();
    private EntityUid? _fishChoicesPart;

    private void ApplyFishState(SurgeryBuiState state)
    {
        TryInitWindow();
        if (_window == null)
            return;

        Entity<OrganComponent>? selected = null;
        if (_part is { } partUid && _entities.TryGetComponent<OrganComponent>(partUid, out var partOrgan) &&
            partOrgan.Body == Owner && SharedSurgerySystem.IsSurgeryTarget(partOrgan))
            selected = (partUid, partOrgan);

        var selectedInChoices = false;
        _fishParts.Clear();
        foreach (var netPart in state.Choices.Keys)
        {
            if (!_entities.TryGetEntity(netPart, out var uid) ||
                !_entities.TryGetComponent<OrganComponent>(uid, out var organ) ||
                organ.Body != Owner || !SharedSurgerySystem.IsSurgeryTarget(organ))
                continue;

            Entity<OrganComponent> part = (uid.Value, organ);
            _fishParts.Add((part, GetPartName(part)));
            selectedInChoices |= uid == _part;
        }
        // Список доступных операций не является списком существующих частей тела.
        if (selected is { } retainedPart && !selectedInChoices)
            _fishParts.Add((retainedPart, GetPartName(retainedPart)));
        _fishParts.Sort((a, b) =>
        {
            var order = SharedSurgerySystem.GetSurgeryTargetScore(a.Part)
                .CompareTo(SharedSurgerySystem.GetSurgeryTargetScore(b.Part));
            return order != 0 ? order : a.Part.Owner.CompareTo(b.Part.Owner);
        });
        _window.SetParts(_fishParts);

        if (selected is not { } selectedPart || !_entities.TryGetNetEntity(selectedPart, out var selectedNet))
        {
            _part = null;
            _surgery = null;
            _previousSurgeries.Clear();
            _window.DismissConfirmation();
            View(ViewType.Parts);
        }
        else if (_surgery is { } surgery && _entities.HasComponent<SurgeryComponent>(surgery.Ent))
        {
            // Вскрытие и завершение меняют Choices; это не пользовательский переход назад.
            // Сами шаги остаются на месте, а допустимость нажатия проверяется отдельно.
            RefreshUI();
            View(ViewType.Steps);
        }
        else
        {
            var choices = state.Choices.GetValueOrDefault(selectedNet.Value) ?? new();
            if (_surgery != null)
            {
                _surgery = null;
                _previousSurgeries.Clear();
                _window.DismissConfirmation();
            }

            if (_fishChoicesPart != selectedPart.Owner || !HaveSameFishChoices(_fishShownChoices, choices))
                OnPartPressed(selectedNet.Value, choices);
            else
            {
                foreach (var (id, _, completed) in choices)
                {
                    if (_fishOperationButtons.TryGetValue(id, out var button))
                        SetFishOperationCompleted(button, completed);
                }
                View(ViewType.Surgeries);
            }
        }

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    /// <summary>Немедленно закрывает подтверждение, если выбранная операция исчезла.</summary>
    internal void InvalidateFishSurgery(EntityUid surgery)
    {
        if (_surgery is not { Ent: var selected } || selected != surgery)
            return;

        _surgery = null;
        _previousSurgeries.Clear();
        ResetFishSessionState();

        if (State is SurgeryBuiState state)
            ApplyFishState(state);
    }

    /// <summary>Completion changes update row styling without rebuilding the operation list.</summary>
    internal static bool HaveSameFishChoices(
        IReadOnlyList<(EntProtoId, string, bool)> previous,
        IReadOnlyList<(EntProtoId, string, bool)> current)
    {
        if (previous.Count != current.Count)
            return false;
        for (var i = 0; i < previous.Count; i++)
        {
            if (previous[i].Item1 != current[i].Item1 || previous[i].Item2 != current[i].Item2)
                return false;
        }
        return true;
    }

    private void RememberFishChoices(List<(EntProtoId, string, bool)> choices)
    {
        _fishChoicesPart = _part;
        _fishShownChoices.Clear();
        _fishShownChoices.AddRange(choices);
        _fishOperationButtons.Clear();
    }

    private static void SetFishOperationCompleted(ChoiceControl button, bool completed)
    {
        if (completed != button.Button.HasStyleClass("FishSurgeryDone"))
        {
            if (completed)
                button.Button.AddStyleClass("FishSurgeryDone");
            else
                button.Button.RemoveStyleClass("FishSurgeryDone");
        }
        button.NameLabel.Modulate = completed ? FishSurgerySheetlet.Palette.TextDark : Color.White;
    }

    private bool ShouldRefreshFishStep(SurgeryStepButton button, StepStatus status)
    {
        var canPerform = true;
        string? popup = null;
        var reason = default(StepInvalidReason);
        if (status == StepStatus.Next && _player.LocalEntity is { } user && _part is { } part)
            canPerform = _system.CanPerformStep(user, Owner, part, button.Step, false, out popup, out reason, out _);

        var presentation = new FishStepPresentation(status, canPerform, popup, reason, IsFishActionBusy(),
            _entities.GetComponent<MetaDataComponent>(button.Step).EntityName,
            _entities.GetComponentOrNull<Robust.Client.GameObjects.SpriteComponent>(button.Step)?.Icon?.Default);
        if (_fishStepPresentations.TryGetValue(button, out var previous) && previous == presentation)
            return false;

        _fishStepPresentations[button] = presentation;
        button.ToolTip = null;
        return true;
    }

    private readonly record struct FishStepPresentation(
        StepStatus Status, bool CanPerform, string? Popup, StepInvalidReason Reason, bool Busy, string Name, Texture? Icon);
}
