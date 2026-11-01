using Robust.Shared.Configuration;

namespace Content.Shared._Fish.DmArena;

[CVarDefs]
public sealed class DmArenaConfigVars
{
    public static readonly CVarDef<bool> DmArenaEnabled =
        CVarDef.Create("dmarena.enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> DmArenaOpenDelay =
        CVarDef.Create("dmarena.open_delay", 20, CVar.SERVERONLY);

    public static readonly CVarDef<bool> DmArenaRefill =
        CVarDef.Create("dmarena.refill", true, CVar.SERVERONLY);
}
