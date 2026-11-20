using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ANHN_O2_N2_Reaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;

        // реакция работает только в диапазоне 50 - 300 K
        if (temperature < 50f || temperature > 300f)
            return ReactionResult.NoReaction;

        var axoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        var hyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var oxygen = mixture.GetMoles(Gas.Oxygen);

        var totalNoblium = axoNoblium + hyperNoblium;
        var totalReactants = nitrogen + oxygen;

        if (totalNoblium < 0.1f || totalReactants < 0.3f)
            return ReactionResult.NoReaction;

        // Баланс ноблиев +-5%
        var ratio = Math.Abs(axoNoblium - hyperNoblium) * 2f / Math.Max(totalNoblium, 0.01f);
        if (ratio > 0.05f)
            return ReactionResult.NoReaction;

        if (totalNoblium > 500f)
            return ReactionResult.NoReaction;

        // чем ближе температура к 50 K - тем быстрее реакция
        var tempFactor = (300f - temperature) * 0.01f + 0.5f;
        tempFactor = Math.Clamp(tempFactor, 0.5f, 5f);

        // параболическое убывание эффективности от количества ноблия
        var nobliumFactor = 1.0f + MathF.Sqrt(totalNoblium);

        // базовая скорость низкая
        var efficiency = 0.05f * tempFactor * nobliumFactor;

        var produceN2O = MathF.Min(nitrogen, oxygen) * efficiency * 0.05f;

        if (produceN2O < 0.001f)
            return ReactionResult.NoReaction;

        // Не даём реакции превратить весь объём за один тик.
        produceN2O = MathF.Min(produceN2O, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // расход только азота и кислорода
        mixture.AdjustMoles(Gas.Nitrogen, -produceN2O * 2f);
        mixture.AdjustMoles(Gas.Oxygen,   -produceN2O);

        // ноблии - катализаторы (не тратятся)
        mixture.AdjustMoles(Gas.NitrousOxide, produceN2O);

        // экзотермическая
        var energyReleased = produceN2O * 1000f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}