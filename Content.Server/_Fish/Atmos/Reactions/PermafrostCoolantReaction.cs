using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class PermafrostCoolantReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        var permafrost = mixture.GetMoles(Gas.Permafrost);

        if (temperature <= 10f || permafrost < 0.05f)
            return ReactionResult.NoReaction;

        // 1. Охлаждающая способность нарастает с температурой выше 50 K
        float coolingPower;
        if (temperature > 50f)
        {
            // Чем выше температура - тем сильнее охлаждает
            coolingPower = 0.1f + (temperature - 50f) * 0.0002f;
            coolingPower = Math.Min(coolingPower, 6.0f);     // максимум при очень высокой температуре
        }
        else
        {
            // Ниже 50 K - минимальная постоянная скорость охлаждения
            coolingPower = 0.1f;
        }

        // Охлаждение
        var energyAbsorbed = permafrost * 500f * coolingPower;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = (temperature * oldHeatCapacity - energyAbsorbed) / newHeatCapacity;
        }

        return ReactionResult.Reacting;
    }
}