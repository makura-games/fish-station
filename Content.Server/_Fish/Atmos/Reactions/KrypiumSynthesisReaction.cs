using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class KrypiumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature > 100f || pressure >= 100f)
            return ReactionResult.NoReaction;

        var healium = mixture.GetMoles(Gas.Healium);
        var bz = mixture.GetMoles(Gas.BZ);
        var plasma = mixture.GetMoles(Gas.Plasma);

        if (healium < 0.3f || bz < 0.3f || plasma < 0.9f)
            return ReactionResult.NoReaction;

        var efficiency = 3.5f;  

        // соотношение: хил : БЗ : плазма примерно равно 1 : 1 : 3
        var maxFromHealium = healium * efficiency;
        var maxFromBz      = bz      * efficiency;
        var maxFromPlasma  = plasma  * (efficiency / 3f);

        var produceKrypium = new[] { maxFromHealium, maxFromBz, maxFromPlasma }.Min();

        if (produceKrypium < 0.05f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produceKrypium = MathF.Min(produceKrypium, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        var consumedHealium = produceKrypium / efficiency;
        var consumedBz      = produceKrypium / efficiency;
        var consumedPlasma  = (produceKrypium / efficiency) * 3f;

        mixture.AdjustMoles(Gas.Healium, -consumedHealium);
        mixture.AdjustMoles(Gas.BZ,      -consumedBz);
        mixture.AdjustMoles(Gas.Plasma,  -consumedPlasma);

        mixture.AdjustMoles(Gas.Krypium, produceKrypium);

        var co2Produced = produceKrypium * 10f;
        mixture.AdjustMoles(Gas.CarbonDioxide, co2Produced);

        // Экзотермическая реакция
        var energyReleased = produceKrypium * 2000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}