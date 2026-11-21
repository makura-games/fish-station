using Content.Server._Fish.StationCentcomm;
using Robust.Shared.IoC;

namespace Content.Server.GameTicking.Rules;

public abstract partial class GameRuleSystem<T> : EntitySystem where T : IComponent
{
    [Dependency] private FishCentcommCheckSystem _fishCentcomm = default!;

    /// <summary>
    ///     True, если сущность находится на карте ЦК. Геймрулы и ивенты не должны туда целиться.
    /// </summary>
    protected bool IsOnCentcomm(EntityUid uid)
    {
        return _fishCentcomm.IsOnCentcomm(uid);
    }
}
