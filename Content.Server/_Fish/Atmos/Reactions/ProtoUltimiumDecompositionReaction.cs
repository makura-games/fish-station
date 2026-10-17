using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using System;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoUltimiumDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var pressure = mixture.Pressure;

        // реакция разложения идёт при давлении < 200 кПа
        if (pressure >= 200f)
            return ReactionResult.NoReaction;

        var protoMoles = mixture.GetMoles(Gas.ProtoUltimium);

        // механика, что и у N2O, но медленнее
        var burnedFuel = protoMoles / 3.0f;   // 3.0f - скорость разложения 

        // Fish-edit: не даём реакции превратить весь объём за один тик.
        burnedFuel = MathF.Min(burnedFuel, mixture.TotalMoles * Atmospherics.MaxConversionFractionPerTick);

        if (burnedFuel <= 0 || protoMoles - burnedFuel < 0)
            return ReactionResult.NoReaction;

        // Убираем Protoultimium
        mixture.AdjustMoles(Gas.ProtoUltimium, -burnedFuel);

        // Разложение: азот + кислород + много плазмы
        mixture.AdjustMoles(Gas.Nitrogen, burnedFuel * 0.85f);
        mixture.AdjustMoles(Gas.Oxygen,   burnedFuel * 0.65f);
        mixture.AdjustMoles(Gas.Plasma,   burnedFuel * 5.0f);   // очень много плазмы

        return ReactionResult.Reacting;
    }
}