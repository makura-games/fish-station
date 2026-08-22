using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BluespaceArtillery;

[Serializable, NetSerializable]
public sealed partial class BluespaceArtilleryDesignatorDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Location { get; private set; }

    [DataField]
    public int FireModeIndex { get; private set; }

    [DataField("fx")]
    public NetEntity? Effect { get; private set; }

    private BluespaceArtilleryDesignatorDoAfterEvent()
    {
    }

    public BluespaceArtilleryDesignatorDoAfterEvent(NetCoordinates location, int fireModeIndex, NetEntity? effect = null)
    {
        Location = location;
        FireModeIndex = fireModeIndex;
        Effect = effect;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
