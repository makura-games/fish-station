using Content.Client._Fish.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client._Starlight;
using SurgeryAction = Content.Shared.DoAfter.DoAfter;

namespace Content.Client._Starlight.Medical.Surgery;

public sealed partial class SurgeryBui
{
    /* Выбор на схеме использует те же операции и серверные сообщения, что и исходный список. */
    private TimeSpan _fishRequestUntil;
    private DoAfterId? _fishPreviousActionId;
    private TimeSpan _fishResultUntil;
    private SurgeryAction? _fishAction;
    private (ushort Id, bool Cancelled, bool Completed)? _fishProgressKey;
    private FishActionContext? _fishRequestedAction;

    private void InitializeFishBodyDiagram(FishSurgeryWindow window)
    {
        window.SetPatient(_entities.GetComponent<MetaDataComponent>(Owner).EntityName, Owner);
        window.PartSelected += OnFishPartSelected;
        window.RefreshRequested += RefreshUI;
        window.ProgressRequested += UpdateFishProgress;
    }

    /// <summary>Сбрасывает transient-состояние между закрытием и повторным открытием BUI.</summary>
    private void ResetFishSessionState()
    {
        _fishRequestUntil = TimeSpan.Zero;
        _fishPreviousActionId = null;
        _fishResultUntil = TimeSpan.Zero;
        _fishAction = null;
        _fishProgressKey = null;
        _fishRequestedAction = null;
        _window?.DismissConfirmation();
        _window?.ClearActionProgress();
    }

    /// <summary>Обновляет схему только после события изменения внешности пациента.</summary>
    internal void RefreshFishAppearance()
    {
        _window?.RefreshPatientAppearance();
    }

    /// <summary>Объединяет частые позиционные события в одно обновление интерфейса.</summary>
    internal void QueueFishUiRefresh()
    {
        _window?.RequestRefresh();
    }

    private void OnFishPartSelected(EntityUid part)
    {
        if (!_entities.TryGetNetEntity(part, out var netPart) ||
            State is not SurgeryBuiState state ||
            !state.Choices.TryGetValue(netPart.Value, out var surgeries))
            return;

        // Не переносим выбранную операцию и её историю на другую конечность.
        _surgery = null;
        _previousSurgeries.Clear();
        OnPartPressed(netPart.Value, surgeries);
    }

    private void RefreshFishAreaCard()
    {
        if (_window == null)
            return;

        if (_part is not { } part || !_entities.TryGetComponent<OrganComponent>(part, out var organ) || organ.Body != Owner)
        {
            _window.SetOperation(null);
            _window.DismissConfirmation();
            return;
        }

        _window.SetAreaStatus(
            _entities.HasComponent<IncisionOpenComponent>(part),
            _entities.HasComponent<SkinRetractedComponent>(part));

        if (_surgery is not { } surgery || !_entities.TryGetComponent<MetaDataComponent>(surgery.Ent, out var meta))
        {
            _window.SetOperation(null);
            return;
        }

        EntityUid? currentStep = null;
        if (_system.GetNextStep(Owner, part, surgery.Ent) is { } next &&
            _entitySystem.TryGetSingleton(next.Surgery.Comp.Steps[next.Step], out var step))
        {
            currentStep = step;
            if (_player.LocalEntity is { } user &&
                !_system.CanPerformStep(user, Owner, part, step, false))
                _window.DismissConfirmation();
        }

        _window.SetOperation(meta.EntityName, currentStep);
    }

    private static void ConfigureFishChoice(ChoiceControl choice, bool completed = false)
    {
        FishSurgeryWindow.ConfigureChoice(choice);
        choice.Button.Modulate = Color.White;
        if (completed)
        {
            choice.Button.AddStyleClass("FishSurgeryDone");
            choice.NameLabel.Modulate = FishSurgerySheetlet.Palette.TextDark;
        }
    }

    private void StyleFishStep(SurgeryStepButton button, StepStatus status)
    {
        _window?.SetStepPresentation(button, status == StepStatus.Next, status == StepStatus.Complete);
        button.Button.Modulate = Color.White;
        button.Button.RemoveStyleClass("FishSurgeryNext");
        button.Button.RemoveStyleClass("FishSurgeryDone");
        if (status == StepStatus.Next)
            button.Button.AddStyleClass("FishSurgeryNext");
        if (status == StepStatus.Complete)
            button.Button.AddStyleClass("FishSurgeryDone");
        button.Button.Disabled |= IsFishActionBusy();
        button.NameLabel.Modulate = button.Button.Disabled ? FishSurgerySheetlet.Palette.TextDark : Color.White;
        if (status == StepStatus.Complete)
            button.ToolTip = _loc.GetString("fish-surgery-step-complete");
    }

    private static FormattedMessage DecorateFishStep(FormattedMessage name, StepStatus status, int index)
    {
        var message = new FormattedMessage();
        message.AddText(status == StepStatus.Complete ? "✓  " : $"{index + 1}.  ");
        message.AddMessage(name);
        return message;
    }

    private void RequestFishStep(NetEntity netPart, EntProtoId surgeryId, EntProtoId stepId)
    {
        if (!CanRequestFishStep(netPart, surgeryId, stepId, out var step))
            return;

        if (_entities.HasComponent<SurgeryStepAmputationEffectComponent>(step) ||
            _entities.HasComponent<SurgeryStepOrganExtractComponent>(step))
        {
            var description = _loc.GetString("fish-surgery-danger-description",
                ("part", GetSelectedPartName() ?? string.Empty),
                ("step", _entities.GetComponent<MetaDataComponent>(step).EntityName));
            _window?.RequestConfirmation(step, description, () => TrySendFishStep(netPart, surgeryId, stepId));
            return;
        }

        TrySendFishStep(netPart, surgeryId, stepId);
    }

    private bool TrySendFishStep(NetEntity netPart, EntProtoId surgeryId, EntProtoId stepId)
    {
        // Подтверждение не сохраняет разрешение: инструмент, этап и конечность проверяются заново.
        if (!CanRequestFishStep(netPart, surgeryId, stepId, out _))
            return false;

        _fishPreviousActionId = FindFishAction()?.Id;
        if (_entities.TryGetEntity(netPart, out var part))
            _fishRequestedAction = new FishActionContext(part.Value, surgeryId, stepId);
        SendMessage(new SurgeryStepChosenBuiMsg { Part = netPart, Surgery = surgeryId, Step = stepId });
        _fishRequestUntil = _game.CurTime + TimeSpan.FromSeconds(2);
        RefreshUI();
        return true;
    }

    private bool CanRequestFishStep(NetEntity netPart, EntProtoId surgeryId, EntProtoId stepId, out EntityUid step)
    {
        step = default;
        if (_window is not { IsOpen: true } || IsFishActionBusy() ||
            _player.LocalEntity is not { } user ||
            _surgery is not { } selected || selected.Proto != surgeryId ||
            !_entities.TryGetEntity(netPart, out var part) || _part != part ||
            State is not SurgeryBuiState state || !state.Choices.ContainsKey(netPart) ||
            !_entities.TryGetComponent<OrganComponent>(part, out var organ) || organ.Body != Owner ||
            !_entities.HasComponent<SurgeryProgressComponent>(part) ||
            !_entities.TryGetComponent<SurgeryComponent>(selected.Ent, out var surgery) ||
            !_entitySystem.TryGetSingleton(stepId, out step))
            return false;

        return _system.IsLyingDown(Owner) &&
            _system.IsSurgeryValid(Owner, part.Value, surgeryId, stepId, out _, out _, out _) &&
            !_system.IsStepComplete(part.Value, surgeryId, stepId) &&
            _system.PreviousStepsComplete(Owner, part.Value, (selected.Ent, surgery), stepId) &&
            _system.CanPerformStep(user, Owner, part.Value, step, false);
    }

    private bool IsFishActionBusy()
    {
        return _game.CurTime < _fishRequestUntil || FindFishAction() is { Cancelled: false, Completed: false };
    }

    private SurgeryAction? FindFishAction()
    {
        if (!_entities.TryGetComponent<DoAfterComponent>(_player.LocalEntity, out var component) ||
            !TryGetFishActionContext(out var context))
            return null;

        return FindFishSurgeryAction(
            component.DoAfters.Values,
            Owner,
            context.Part,
            context.Surgery,
            context.Step);
    }

    private bool TryGetFishActionContext(out FishActionContext context)
    {
        if (_fishRequestedAction is { } requested)
        {
            context = requested;
            return true;
        }

        if (_part is not { } part || _surgery is not { } surgery ||
            !_entities.TryGetComponent<SurgeryComponent>(surgery.Ent, out var surgeryComponent) ||
            _system.GetNextStep(Owner, part, surgery.Ent) is not { } next ||
            next.Surgery.Owner != surgery.Ent)
        {
            context = default;
            return false;
        }

        context = new FishActionContext(part, surgery.Proto, surgeryComponent.Steps[next.Step]);
        return true;
    }

    private void UpdateFishProgress()
    {
        if (_window == null)
            return;

        var action = FindFishAction();
        if (action != null && _game.CurTime < _fishRequestUntil && action.Id == _fishPreviousActionId)
        {
            _window.SetActionProgress(null, _loc.GetString("fish-surgery-waiting"), 0);
            return;
        }

        if (action == null)
        {
            if (_fishAction != null)
            {
                var finishedStep = GetFishActionStep(_fishAction);
                if (!_fishAction.Cancelled)
                    _window.SetActionProgress(finishedStep, _loc.GetString("fish-surgery-action-updated"), 1);
                _fishResultUntil = _game.CurTime + TimeSpan.FromSeconds(2);
                _fishAction = null;
                _fishProgressKey = null;
                _fishRequestedAction = null;
            }

            if (_game.CurTime < _fishRequestUntil)
                _window.SetActionProgress(null, _loc.GetString("fish-surgery-waiting"), 0);
            else if (_game.CurTime >= _fishResultUntil)
            {
                _fishRequestedAction = null;
                _window.ClearActionProgress();
            }
            return;
        }

        _fishRequestUntil = TimeSpan.Zero;
        _fishAction = action;
        var now = _game.CurTime;
        if (_player.LocalEntity is { } user)
            now -= _entities.System<MetaDataSystem>().GetPauseTime(user);

        var fraction = GetFishProgress(action, now);
        var actionStep = GetFishActionStep(action);
        _window.SetActionFraction(actionStep, fraction);
        var key = (action.Index, action.Cancelled, action.Completed);
        if (_fishProgressKey == key)
            return;

        _fishProgressKey = key;
        var ev = (SurgeryDoAfterEvent)action.Args.Event;
        var name = _entitySystem.TryGetSingleton(ev.Step, out var step) &&
            _entities.TryGetComponent<MetaDataComponent>(step, out var meta) ? meta.EntityName : ev.Step.Id;
        var partName = action.Args.Target is { } target &&
            _entities.TryGetComponent<OrganComponent>(target, out var organ)
            ? GetPartName((target, organ)) : _loc.GetString("fish-surgery-unknown-area");
        var caption = _loc.GetString(action.Cancelled ? "fish-surgery-action-cancelled" :
            action.Completed ? "fish-surgery-action-finished" : "fish-surgery-action-running",
            ("step", name), ("part", partName));
        _window.SetActionProgress(actionStep, caption, fraction, action.Cancelled);
        if (!action.Cancelled && !action.Completed)
            _window.DismissConfirmation();
    }

    private EntityUid? GetFishActionStep(SurgeryAction action)
    {
        var ev = (SurgeryDoAfterEvent) action.Args.Event;
        return _entitySystem.TryGetSingleton(ev.Step, out var step) ? step : null;
    }

    /// <summary>Находит последнее действие для точной комбинации пациента, части тела, операции и этапа.</summary>
    internal static SurgeryAction? FindFishSurgeryAction(
        IEnumerable<SurgeryAction> actions,
        EntityUid patient,
        EntityUid part,
        EntProtoId surgery,
        EntProtoId step)
    {
        SurgeryAction? latest = null;
        foreach (var action in actions)
        {
            if (!IsFishSurgeryAction(action, patient, part, surgery, step))
                continue;

            if (!action.Cancelled && !action.Completed)
                return action;
            if (latest == null || action.StartTime > latest.StartTime)
                latest = action;
        }

        return latest;
    }

    /// <summary>Исключает чужие хирургические действия над тем же пациентом.</summary>
    internal static bool IsFishSurgeryAction(
        SurgeryAction action,
        EntityUid patient,
        EntityUid part,
        EntProtoId surgery,
        EntProtoId step)
    {
        return action.Args.Event is SurgeryDoAfterEvent surgeryEvent &&
            action.Args.EventTarget == patient &&
            action.Args.Target == part &&
            surgeryEvent.Surgery == surgery &&
            surgeryEvent.Step == step;
    }

    /// <summary>Freezes cancellation at its actual time and handles instantaneous steps.</summary>
    internal static float GetFishProgress(SurgeryAction action, TimeSpan now)
        => GetFishProgress(action.StartTime, action.Args.Delay, action.CancelledTime, now);

    internal static float GetFishProgress(TimeSpan start, TimeSpan delay, TimeSpan? cancelled, TimeSpan now)
    {
        var elapsed = (cancelled ?? now) - start;
        return delay <= TimeSpan.Zero ? 1f :
            Math.Clamp((float)(elapsed.TotalSeconds / delay.TotalSeconds), 0f, 1f);
    }

    private readonly record struct FishActionContext(EntityUid Part, EntProtoId Surgery, EntProtoId Step);
}
