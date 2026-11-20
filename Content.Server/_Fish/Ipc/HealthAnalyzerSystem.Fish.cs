using Content.Server.Medical.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Medical;

public sealed partial class HealthAnalyzerSystem
{
    // Fish-start: пустой фильтр сканирует силиконов, как робо-анализатор боргов.
    // Biological-only медсканер КПБ не читает. Mech/Silicon/Ipc — читают.
    private static bool AcceptsFishIpc(HealthAnalyzerComponent analyzer)
    {
        if (analyzer.DamageContainers is not { Count: > 0 } containers)
            return true;

        foreach (var container in containers)
        {
            if (container == "Ipc"
                || container == "Silicon"
                || container == "Synth"
                || container == "Xenoborg"
                || container == "Mech")
                return true;
        }

        return false;
    }
    // Fish-end
}
