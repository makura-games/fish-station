using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class KlemennonSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature > 100f || pressure < 2000f)
            return ReactionResult.NoReaction;

        var garodin = mixture.GetMoles(Gas.Garodin);
        var n2o = mixture.GetMoles(Gas.NitrousOxide);
        var nitrium = mixture.GetMoles(Gas.Nitrium);

        if (garodin < 0.15f || n2o < 0.15f || nitrium < 0.15f)
            return ReactionResult.NoReaction;

        // нитры не более 30%
        if (nitrium / mixture.TotalMoles > 0.30f)
            return ReactionResult.NoReaction;

        // чем больше нитры - тем меньше расходуется N2O
        var nitriumRatio = Math.Clamp(nitrium / mixture.TotalMoles, 0.20f, 0.30f);
        var n2oConsumptionFactor = 1f - (nitriumRatio - 0.20f) * 3f; // от 1.0 до 0.7

        var efficiency = 0.5f;

        var produce = new[] 
        { 
            garodin * efficiency,
            n2o * efficiency * n2oConsumptionFactor,
            nitrium * efficiency 
        }.Min();

        if (produce < 0.07f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        mixture.AdjustMoles(Gas.Garodin, -produce * 0.95f);
        mixture.AdjustMoles(Gas.NitrousOxide, -produce * n2oConsumptionFactor);
        mixture.AdjustMoles(Gas.Nitrium, -produce * 0.95f);

        mixture.AdjustMoles(Gas.Klemennon, produce);

        // легкий нагрев
        var energyReleased = produce * 2100f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}