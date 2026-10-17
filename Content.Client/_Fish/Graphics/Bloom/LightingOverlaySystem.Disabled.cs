using Robust.Shared.Reflection;

namespace Content.Client._Sunrise.Shaders.Bloom;

// Сохраняем реализацию апстрима, но не запускаем её одновременно со свечением Fish.
[Reflect(false)]
public sealed partial class LightingOverlaySystem;
