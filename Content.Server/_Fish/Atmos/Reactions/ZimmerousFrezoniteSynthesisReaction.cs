using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ZimmerousFrezoniteSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;
            
        var temperature = mixture.Temperature;
        if (temperature > 50f)
            return ReactionResult.NoReaction;

        var frezon = mixture.GetMoles(Gas.Frezon);
        var zimmera = mixture.GetMoles(Gas.Zimmera);

        if (frezon < 0.2f || zimmera < 0.2f)
            return ReactionResult.NoReaction;

        // 50/50 смесь
        var efficiency = 0.1f;

        var produce = MathF.Min(frezon, zimmera) * efficiency;

        if (produce < 0.08f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // расход 50/50
        mixture.AdjustMoles(Gas.Frezon, -produce * 2f);
        mixture.AdjustMoles(Gas.Zimmera, -produce * 2f);

        // производим
        mixture.AdjustMoles(Gas.ZimmerousFrezonite, produce);

        // сильно эндотермическая
        var energyAbsorbed = produce * 25000f;

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = Math.Max(mixture.Temperature - (energyAbsorbed / heatCap), Atmospherics.TCMB);
        }

        return ReactionResult.Reacting;
    }
}