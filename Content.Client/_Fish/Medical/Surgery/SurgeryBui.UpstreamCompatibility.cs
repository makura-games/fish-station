using System.Collections.Generic;
using Content.Client._Fish.Medical.Surgery;
using Content.Client._Starlight;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Medical.Surgery;

/// <summary>
/// Сосредотачивает Fish-настройку хирургического интерфейса в проектной папке.
/// В upstream-файле остаются только короткие вызовы этих точек интеграции.
/// </summary>
public sealed partial class SurgeryBui
{
    private FishSurgeryWindow CreateFishWindow()
    {
        var window = new FishSurgeryWindow();
        InitializeFishBodyDiagram(window);
        return window;
    }

    private List<(EntProtoId, string, bool)> GetFishChoices(SurgeryBuiState state, NetEntity part)
        => state.Choices.GetValueOrDefault(part) ?? new();

    private void InitializeFishStep(
        SurgeryStepButton button,
        NetEntity part,
        EntProtoId surgery,
        EntProtoId step)
    {
        ConfigureFishChoice(button);
        _window?.RegisterStep(button);
        button.Button.OnPressed += _ => RequestFishStep(part, surgery, step);
    }

    private void PrepareFishStepsForOperation()
    {
        _window?.ResetStepHistory();
        _fishStepPresentations.Clear();
    }

    private static void InitializeFishRequirement(ChoiceControl choice)
        => ConfigureFishChoice(choice);

    private void InitializeFishOperation(ChoiceControl choice, EntProtoId surgery, bool completed)
    {
        ConfigureFishChoice(choice, completed);
        _fishOperationButtons[surgery] = choice;
    }

    private bool PrepareFishStepPresentation(SurgeryStepButton button, ref StepStatus status)
    {
        return ShouldRefreshFishStep(button, status);
    }

    private void ApplyFishStepPresentation(
        SurgeryStepButton button,
        FormattedMessage name,
        StepStatus status,
        int index,
        Texture? texture)
    {
        button.Set(DecorateFishStep(name, status, index), texture);
        StyleFishStep(button, status);
    }

    private void ApplyFishView(ViewType type)
    {
        if (_window == null)
            return;

        _window.ShowSelection(_part, GetSelectedPartName(), type == ViewType.Parts, type == ViewType.Steps);
        RefreshFishAreaCard();
    }
}
