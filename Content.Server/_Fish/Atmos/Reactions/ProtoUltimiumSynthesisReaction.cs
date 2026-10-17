using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoUltimiumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var pressure = mixture.Pressure;
        if (pressure < 8000f)
            return ReactionResult.NoReaction;

        var vapor = mixture.GetMoles(Gas.WaterVapor);
        var garodin = mixture.GetMoles(Gas.Garodin);
        var ammonia = mixture.GetMoles(Gas.Ammonia);

        if (vapor < 0.4f || garodin < 0.25f || ammonia < 0.15f)
            return ReactionResult.NoReaction;

        var efficiency = 0.5f;

        var produce = new[] 
        { 
            vapor * efficiency * 0.9f,
            garodin * efficiency,
            ammonia * efficiency 
        }.Min();

        if (produce < 0.09f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // Расход
        mixture.AdjustMoles(Gas.WaterVapor, -produce * 2.0f);
        mixture.AdjustMoles(Gas.Garodin,    -produce * 0.8f);
        mixture.AdjustMoles(Gas.Ammonia,    -produce * 0.9f);

        // Производим протоультимиум
        mixture.AdjustMoles(Gas.ProtoUltimium, produce);

        // сильно эндотермическая
        var energyAbsorbed = produce * 1850f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = Math.Max(mixture.Temperature - (energyAbsorbed / heatCap), Atmospherics.TCMB);
        }

        return ReactionResult.Reacting;
    }
}