using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ANHN_WV_Reaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var pressure = mixture.Pressure;
        if (pressure > 100f)
            return ReactionResult.NoReaction;

        var vapor = mixture.GetMoles(Gas.WaterVapor);
        var axoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        var hyperNoblium = mixture.GetMoles(Gas.HyperNoblium);

        if (vapor < 0.7f || axoNoblium < 0.12f || hyperNoblium < 0.12f)
            return ReactionResult.NoReaction;

        var efficiency = 0.5f;

        // пар тратится сильно, ноблии - умеренно
        var maxFromVapor = vapor * efficiency * 0.2f;
        var maxFromAxo = axoNoblium * efficiency;
        var maxFromHyper = hyperNoblium * efficiency;

        var produceHalon = new[] { maxFromVapor, maxFromAxo, maxFromHyper }.Min();

        if (produceHalon < 0.08f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produceHalon = MathF.Min(produceHalon, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // расход
        mixture.AdjustMoles(Gas.WaterVapor,    -produceHalon * 3f);   // большой расход пара
        mixture.AdjustMoles(Gas.AxoNoblium,   -produceHalon * 0.5f);
        mixture.AdjustMoles(Gas.HyperNoblium,  -produceHalon * 0.5f);

        // производим Halon
        mixture.AdjustMoles(Gas.Halon, produceHalon);

        return ReactionResult.Reacting;
    }
}