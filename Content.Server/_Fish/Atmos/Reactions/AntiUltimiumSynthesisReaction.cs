using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class AntiUltimiumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < 40000f)
            return ReactionResult.NoReaction;

        var zenthium = mixture.GetMoles(Gas.Zenthium);
        var krypium  = mixture.GetMoles(Gas.Krypium);
        var prallium = mixture.GetMoles(Gas.Prallium);
        var chaoson  = mixture.GetMoles(Gas.Chaoson);
        var baratrium = mixture.GetMoles(Gas.Baratrium);
        var ethylium = mixture.GetMoles(Gas.Ethylium);
        var zimmera  = mixture.GetMoles(Gas.Zimmera);
        var framel   = mixture.GetMoles(Gas.Framel);
        var klemennon = mixture.GetMoles(Gas.Klemennon);
        var protoult = mixture.GetMoles(Gas.ProtoUltimium);
        var halon = mixture.GetMoles(Gas.Halon);

        // Все 11 газов должны присутствовать в примерно равных количествах
        if (zenthium < 0.08f || krypium < 0.08f || prallium < 0.08f || chaoson < 0.08f ||
            baratrium < 0.08f || ethylium < 0.08f || zimmera < 0.08f || framel < 0.08f ||
            klemennon < 0.08f || protoult < 0.08f || halon < 0.08f)
            return ReactionResult.NoReaction;

        var efficiency = 3.0f;

        var produce = new[]
        {
            zenthium * efficiency,
            krypium * efficiency,
            prallium * efficiency,
            chaoson * efficiency,
            baratrium * efficiency,
            ethylium * efficiency,
            zimmera * efficiency,
            framel * efficiency,
            klemennon * efficiency,
            protoult * efficiency,
            halon * efficiency
        }.Min();

        if (produce < 0.1f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        // Каждый из 11 газов расходуется на `produce`, поэтому ограничиваем его ещё и
        // десятой частью запаса лимитирующего реагента — иначе одного минимума мало,
        // ведь `produce` считается с коэффициентом эффективности 3.0.
        var maxReagentConversion = new[]
        {
            zenthium,
            krypium,
            prallium,
            chaoson,
            baratrium,
            ethylium,
            zimmera,
            framel,
            klemennon,
            protoult,
            halon
        }.Min() * Atmospherics.MaxConversionFractionPerTick;
        produce = MathF.Min(produce, maxReagentConversion);
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        // Расход всех 11 газов (примерно поровну)
        mixture.AdjustMoles(Gas.Zenthium, -produce);
        mixture.AdjustMoles(Gas.Krypium, -produce);
        mixture.AdjustMoles(Gas.Prallium, -produce);
        mixture.AdjustMoles(Gas.Chaoson, -produce);
        mixture.AdjustMoles(Gas.Baratrium, -produce);
        mixture.AdjustMoles(Gas.Ethylium, -produce);
        mixture.AdjustMoles(Gas.Zimmera, -produce);
        mixture.AdjustMoles(Gas.Framel, -produce);
        mixture.AdjustMoles(Gas.Klemennon, -produce);
        mixture.AdjustMoles(Gas.ProtoUltimium, -produce);
        mixture.AdjustMoles(Gas.Halon, -produce);

        // Производим AntiUltimium
        mixture.AdjustMoles(Gas.AntiUltimium, produce);

        // Слегка экзотермическая
        var energyReleased = produce * 2800f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}