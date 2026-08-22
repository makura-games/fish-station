using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ChaosonSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature > 50f || pressure < 1500f)
            return ReactionResult.NoReaction;

        var n2o = mixture.GetMoles(Gas.NitrousOxide);
        var frezon = mixture.GetMoles(Gas.Frezon);

        if (n2o < 1.0f || frezon < 0.7f)
            return ReactionResult.NoReaction;

        var efficiency = 2.5f;

        var maxFromN2O = n2o * efficiency;
        var maxFromFrezon = frezon * efficiency;

        var produce = new[] { maxFromN2O, maxFromFrezon }.Min();

        if (produce < 0.12f)
            return ReactionResult.NoReaction;

        var consumedN2O = produce / efficiency;
        var consumedFrezon = produce / efficiency;

        mixture.AdjustMoles(Gas.NitrousOxide, -consumedN2O);
        mixture.AdjustMoles(Gas.Frezon, -consumedFrezon);

        mixture.AdjustMoles(Gas.Chaoson, produce);

        // легкий нагрев
        var energyReleased = produce * 1200f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}