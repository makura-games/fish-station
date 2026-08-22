using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PermafrostDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var permafrost = mixture.GetMoles(Gas.Permafrost);
        var tritium = mixture.GetMoles(Gas.Tritium);

        if (temperature >= 50f || permafrost < 0.05f)
            return ReactionResult.NoReaction;

        // медленное разложение
        var decompRate = (50f - temperature) * 0.5f;   

        if (tritium > 0.04f)
        {
            // при наличии трития - расходуется тритий вместо пермафроста
            var tritConsumed = Math.Min(decompRate * 0.75f, tritium);
            mixture.AdjustMoles(Gas.Tritium, -tritConsumed);
        }
        else
        {
            // Разложение самого пермафроста
            mixture.AdjustMoles(Gas.Permafrost, -decompRate);
            mixture.AdjustMoles(Gas.Frezon, decompRate * 0.8f);
        }

        // слабый нагрев (чтобы не была самоподдерживающейся, хотя делал давно, логику уже и не помню этой строки, может стоит убрать)
        var energyReleased = decompRate * 250f;

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}