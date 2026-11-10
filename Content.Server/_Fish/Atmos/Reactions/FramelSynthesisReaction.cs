using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class FramelSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        if (temperature < 100f || temperature > 200f)
            return ReactionResult.NoReaction;

        var garodin = mixture.GetMoles(Gas.Garodin);
        var healium = mixture.GetMoles(Gas.Healium);
        var nitrium = mixture.GetMoles(Gas.Nitrium);
        var pluoxium = mixture.GetMoles(Gas.Pluoxium);
        var bz = mixture.GetMoles(Gas.BZ);

        if (garodin < 0.15f || healium < 0.3f || nitrium < 0.3f || pluoxium < 0.05f)
        {
            return ReactionResult.NoReaction;
        }

        var pluoxiumFactor = Math.Clamp(pluoxium * 0.05f, 0.1f, 1f);

        var efficiency = 0.25f * pluoxiumFactor;

        var produceFramel = new[] { garodin * efficiency, healium * efficiency, nitrium * efficiency, pluoxium * 10f * efficiency }.Min();

        if (produceFramel < 0.08f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produceFramel = MathF.Min(produceFramel, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // расход основных реагентов
        mixture.AdjustMoles(Gas.Garodin, -produceFramel * 1.05f);
        mixture.AdjustMoles(Gas.Healium, -produceFramel * 1.25f);
        mixture.AdjustMoles(Gas.Nitrium, -produceFramel * 1.25f);

        mixture.AdjustMoles(Gas.Pluoxium, -produceFramel * 0.10f);

        // Производство Фрамель
        mixture.AdjustMoles(Gas.Framel, produceFramel);

        // БЗ остаётся катализатором
        var ammoniaProduced = 0f;

        if (bz >= 0.05f)
        {
            var bzRatio = Math.Clamp(bz / mixture.TotalMoles, 0.05f, 0.10f);
            ammoniaProduced = produceFramel * 2f * (1f + bzRatio * 5f);
        }

        mixture.AdjustMoles(Gas.Ammonia, ammoniaProduced);

        // небольшой нагрев
        var energyReleased = produceFramel * 1450f + ammoniaProduced * 600f;

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}