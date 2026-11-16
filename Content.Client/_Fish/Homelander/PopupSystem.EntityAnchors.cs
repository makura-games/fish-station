// ReSharper disable once CheckNamespace
namespace Content.Client.Popups;

public sealed partial class PopupSystem
{
    internal IEnumerable<(WorldPopupLabel Label, EntityUid? Entity)> EnumerateWorldLabelsWithAnchors()
    {
        foreach (var (data, label) in _aliveWorldLabels)
            yield return (label, data.Entity);
    }
}
