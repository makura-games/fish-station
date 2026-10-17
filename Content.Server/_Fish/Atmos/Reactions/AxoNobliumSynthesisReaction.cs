using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;
using System.Linq;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class AxoNobliumSynthesisReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var pressure = mixture.Pressure;

        if (temperature < 5000f || pressure < 8500f)
            return ReactionResult.NoReaction;

        var garodin = mixture.GetMoles(Gas.Garodin);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);

        if (garodin < 0.25f || hydrogen < 0.08f)
            return ReactionResult.NoReaction;

        // чем выше температура - тем быстрее реакция
        var tempFactor = (temperature - 5000f) * 0.00001f;
        tempFactor = Math.Min(tempFactor, 3.5f);

        // чем выше давление - тем медленнее реакция
        var pressureFactor = Math.Clamp(12000f / pressure, 0.4f, 1.2f);

        var efficiency = 2.0f * tempFactor * pressureFactor;

        // соотношение 3:1 (гародин : водород) => 4 моля аксоноблия
        var maxFromGarodin = garodin * efficiency / 3f;
        var maxFromHydrogen = hydrogen * efficiency;

        var produce = MathF.Min(maxFromGarodin, maxFromHydrogen);

        if (produce < 0.1f)
            return ReactionResult.NoReaction;

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        produce = MathF.Min(produce, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        mixture.AdjustMoles(Gas.Garodin, -produce * 0.75f);
        mixture.AdjustMoles(Gas.Hydrogen, -produce * 0.25f);

        mixture.AdjustMoles(Gas.AxoNoblium, produce);

        // экзотермическая
        var energyReleased = produce * 2400f;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (heatCap > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature += energyReleased / heatCap;
        }

        return ReactionResult.Reacting;
    }
}