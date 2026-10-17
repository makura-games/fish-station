using Robust.Shared.Reflection;

namespace Content.Client._Sunrise.Shaders.Bloom;

// Не обновляем дерево старого свечения, пока его система отключена.
[Reflect(false)]
public sealed partial class BloomOverlayTreeSystem;
