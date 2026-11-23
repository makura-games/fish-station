using System.Linq;
using Content.Shared._Fish.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    [Dependency] private FishActionParticleSystem _fishActionParticle = default!;

    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";

    private void EmitWeldingParticles(EntityUid? target, TimeSpan delay, IEnumerable<ProtoId<ToolQualityPrototype>> qualities)
    {
        if (target is {} workpiece && delay.TotalSeconds >= 0.5 && qualities.Contains(Welding))
            _fishActionParticle.Emit(workpiece, FishParticleKind.Weld);
    }
}
