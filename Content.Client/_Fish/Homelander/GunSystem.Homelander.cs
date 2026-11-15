using Content.Client._Fish.Homelander;

// ReSharper disable once CheckNamespace
namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private HomelanderSystem _homelander = default!;
}
