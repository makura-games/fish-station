using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class GarodinSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < 1000f || temperature > 2000f)
            return ReactionResult.NoReaction;

        var plasma = mixture.GetMoles(Gas.Plasma);
        var frezon = mixture.GetMoles(Gas.Frezon);

        if (plasma < 0.05f || frezon < 0.05f)
            return ReactionResult.NoReaction;

        var efficiency = 3.0f;

        var maxFromPlasma = plasma * efficiency * 0.75f;
        var maxFromFrezon = frezon * efficiency * 0.25f;

        var produce = new[] { maxFromPlasma, maxFromFrezon }.Min();

        if (produce < 0.08f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // Потребляем
        var consPlasma = produce / efficiency * 0.75f * 1.2f;
        var consFrezon = produce / efficiency * 0.25f;

        mixture.AdjustMoles(Gas.Plasma, -consPlasma);
        mixture.AdjustMoles(Gas.Frezon, -consFrezon);

        mixture.AdjustMoles(Gas.Garodin, produce);

        // сильно эндотермическая
        var energyAbsorbed = produce * 12000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = Math.Max(mixture.Temperature - (energyAbsorbed / heatCap), Atmospherics.TCMB);
        }

        return ReactionResult.Reacting;
    }
}