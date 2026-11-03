using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class RiminonSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {

        var initialAxoNoblium = mixture.GetMoles(Gas.AxoNoblium);
        if (initialAxoNoblium >= 5.0f)
            return ReactionResult.NoReaction;

        var bz = mixture.GetMoles(Gas.BZ);
        var frezon = mixture.GetMoles(Gas.Frezon);
        var healium = mixture.GetMoles(Gas.Healium);
        var nitrium = mixture.GetMoles(Gas.Nitrium);

        // все галлюциногенные газы
        if (bz < 0.2f || frezon < 0.2f || healium < 0.15f || nitrium < 0.15f)
            return ReactionResult.NoReaction;

        var totalHallu = bz + frezon + healium + nitrium;
        var efficiency = 1.5f;

        // Превращаем постепенно — не более MaxConversionFractionPerTick от каждого газа за тик.
        var conversion = Atmospherics.MaxConversionFractionPerTick;
        var produce = totalHallu * efficiency * conversion;

        if (produce < 0.1f)
            return ReactionResult.NoReaction;

        // расход всех галлюциногенов
        mixture.AdjustMoles(Gas.BZ, -bz * conversion);
        mixture.AdjustMoles(Gas.Frezon, -frezon * conversion);
        mixture.AdjustMoles(Gas.Healium, -healium * conversion);
        mixture.AdjustMoles(Gas.Nitrium, -nitrium * conversion);

        mixture.AdjustMoles(Gas.Riminon, produce);

        // хотелось бы легкий рандомный нагрев/охлаждение, но что-то с этой функцией не разобрался, а инет вот выдал такое и оно не работает, увы
        // var energy = produce * (800f + (float)Robust.Shared.Random._random.Next(-600, 600));
        var energy = produce * 800f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energy / heatCap;
        }

        return ReactionResult.Reacting;
    }
}