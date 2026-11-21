// ReSharper disable once CheckNamespace
namespace Content.Shared.Antag;

public sealed partial class AntagSpecifierPrototype
{
    /// <summary>
    /// Разрешает КПБ (<c>FishIpcComponent</c>) быть этим антагонистом (NukeOps и т.п.).
    /// </summary>
    [DataField]
    public bool AllowFishIpc;
}
