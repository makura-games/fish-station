using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ZenthiumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;

        if (temperature < 500f)
            return ReactionResult.NoReaction;

        var healiumMoles = mixture.GetMoles(Gas.Healium);
        var frezonMoles = mixture.GetMoles(Gas.Frezon);

        if (healiumMoles < 0.2f || frezonMoles < 0.8f)
            return ReactionResult.NoReaction;

        var maxFromHealium = healiumMoles * 1.8f;
        var maxFromFrezon  = frezonMoles   / 4f * 1.8f;

        var produceAmount = MathF.Min(maxFromHealium, maxFromFrezon);

        if (produceAmount < 0.05f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produceAmount = MathF.Min(produceAmount, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        var healiumConsumed = produceAmount / 1.8f;
        var frezonConsumed  = produceAmount * 4f / 1.8f;

        mixture.AdjustMoles(Gas.Healium, -healiumConsumed);
        mixture.AdjustMoles(Gas.Frezon,  -frezonConsumed);

        mixture.AdjustMoles(Gas.Zenthium, produceAmount);

        // эндотермическая
        var energyAbsorbed = produceAmount * 1000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            var deltaTemp = -energyAbsorbed / heatCap;
            mixture.Temperature = Math.Max(mixture.Temperature + deltaTemp, Atmospherics.TCMB);
        }

        return ReactionResult.Reacting;
    }
}