using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PralliumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature < 5000f || pressure >= 800f)
            return ReactionResult.NoReaction;

        var oxygen = mixture.GetMoles(Gas.Oxygen);
        var co2 = mixture.GetMoles(Gas.CarbonDioxide);
        var nitrium = mixture.GetMoles(Gas.Nitrium);
        var pluoxium = mixture.GetMoles(Gas.Pluoxium);

        if (oxygen < 0.35f || co2 < 0.18f || nitrium < 0.18f || pluoxium < 0.12f)
            return ReactionResult.NoReaction;

        // соотношение: Oxygen : CO2 : Nitrium : Pluoxium ≈ 2.5 : 1.5 : 1 : 0.8
        var efficiency = 2.0f;

        var maxFromOxy = oxygen / 2.5f * efficiency;
        var maxFromCo2 = co2 / 1.5f * efficiency;
        var maxFromNit = nitrium * efficiency;
        var maxFromPluox = pluoxium / 0.8f * efficiency;

        var produce = new[] { maxFromOxy, maxFromCo2, maxFromNit, maxFromPluox }.Min();

        if (produce < 0.06f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // Потребляем реагенты
        var consOxy = produce / efficiency * 2.5f;
        var consCo2 = produce / efficiency * 1.5f;
        var consNit = produce / efficiency;
        var consPluox = produce / efficiency * 0.8f;

        mixture.AdjustMoles(Gas.Oxygen, -consOxy);
        mixture.AdjustMoles(Gas.CarbonDioxide, -consCo2);
        mixture.AdjustMoles(Gas.Nitrium, -consNit);
        mixture.AdjustMoles(Gas.Pluoxium, -consPluox);

        // Производим праллиум
        mixture.AdjustMoles(Gas.Prallium, produce);

        // резко эндотермическая
        var energyAbsorbed = produce * 10000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = Math.Max(mixture.Temperature - (energyAbsorbed / heatCap), Atmospherics.TCMB);
        }

        return ReactionResult.Reacting;
    }
}