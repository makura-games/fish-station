using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class UltimiumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature > 20f)
            return ReactionResult.NoReaction;

        var antiultimium = mixture.GetMoles(Gas.AntiUltimium);
        if (antiultimium < 0.12f)
            return ReactionResult.NoReaction;

        // чем ниже температура - тем быстрее реакция
        var efficiency = (25f - temperature) * 0.06f;   // очень сильная зависимость от холода

        var produce = antiultimium * efficiency * 0.5f;

        if (produce < 0.05f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        mixture.AdjustMoles(Gas.AntiUltimium, -produce * 0.9f);

        mixture.AdjustMoles(Gas.Ultimium, produce);

        // слегка экзотермическая
        var energyReleased = produce * 2000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}