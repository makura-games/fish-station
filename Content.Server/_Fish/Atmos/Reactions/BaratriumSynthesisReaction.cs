using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class BaratriumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature < 500f || temperature > 600f)
            return ReactionResult.NoReaction;

        var tritium = mixture.GetMoles(Gas.Tritium);
        var garodin = mixture.GetMoles(Gas.Garodin);

        if (tritium < 0.08f || garodin < 0.08f)
            return ReactionResult.NoReaction;

        var efficiency = 3.0f;

        var maxFromTritium = tritium * efficiency;
        var maxFromGarodin = garodin * efficiency;

        var produce = new[] { maxFromTritium, maxFromGarodin }.Min();

        if (produce < 0.1f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        var consTritium = produce / efficiency;
        var consGarodin  = produce / efficiency;

        mixture.AdjustMoles(Gas.Tritium, -consTritium);
        mixture.AdjustMoles(Gas.Garodin, -consGarodin);

        mixture.AdjustMoles(Gas.Baratrium, produce);

        // Немного азота как побочка
        mixture.AdjustMoles(Gas.Nitrogen, produce * 0.15f);

        // Слабый нагрев
        var energyReleased = produce * 2000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}