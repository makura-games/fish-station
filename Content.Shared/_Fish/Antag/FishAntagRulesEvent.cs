using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Antag;

[Serializable, NetSerializable]
public sealed class FishAntagRulesEvent : EntityEventArgs
{
    public string[] Rules { get; }

    public FishAntagRulesEvent(string[] rules)
    {
        Rules = rules;
    }
}
