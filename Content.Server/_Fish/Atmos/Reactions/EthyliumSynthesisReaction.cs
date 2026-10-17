using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class EthyliumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var pressure = mixture.Pressure;
        if (pressure < 400f)
            return ReactionResult.NoReaction;

        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var vapor    = mixture.GetMoles(Gas.WaterVapor);
        var garodin  = mixture.GetMoles(Gas.Garodin);
        var prallium = mixture.GetMoles(Gas.Prallium);

        if (nitrogen < 0.15f || vapor < 0.15f || garodin < 0.15f || prallium < 0.25f)
            return ReactionResult.NoReaction;

        var pressureFactor = Math.Clamp(pressure / 800f, 0.3f, 2.5f);

        var efficiency = 3.5f * pressureFactor;

        var maxFromN2     = nitrogen * efficiency;
        var maxFromVapor  = vapor    * efficiency;
        var maxFromGarodin = garodin * efficiency * 0.6f;

        var produce = new[] { maxFromN2, maxFromVapor, maxFromGarodin }.Min();

        if (produce < 0.12f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // расход реагентов
        mixture.AdjustMoles(Gas.Nitrogen,   -produce / efficiency);
        mixture.AdjustMoles(Gas.WaterVapor, -produce / efficiency);
        mixture.AdjustMoles(Gas.Garodin,    -produce / efficiency * 0.45f);
        mixture.AdjustMoles(Gas.Prallium,   -produce / efficiency * 0.08f);

        mixture.AdjustMoles(Gas.Ethylium, produce);

        // слабый нагрев
        var energyReleased = produce * 1200f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}