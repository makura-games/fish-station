using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Собирает пулы gold crit из прототипов с <see cref="FishSlimeExtractGoldCoreSpawnableComponent"/>.
/// </summary>
public sealed class FishSlimeExtractGoldCritPoolSystem : EntitySystem
{
    private readonly List<string> _hostile = new();
    private readonly List<string> _lesser = new();
    private readonly List<string> _friendly = new();

    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
        RebuildPools();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.ByType.ContainsKey(typeof(EntityPrototype)))
            RebuildPools();
    }

    private void RebuildPools()
    {
        _hostile.Clear();
        _lesser.Clear();
        _friendly.Clear();

        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.HasComponent<FishSlimeExtractGoldCoreSpawnableComponent>())
                continue;

            var reg = EntityManager.ComponentFactory.GetRegistration(typeof(FishSlimeExtractGoldCoreSpawnableComponent));
            if (!proto.Components.TryGetComponent(reg.Name, out var compEntry))
                continue;

            var spawn = (FishSlimeExtractGoldCoreSpawnableComponent)compEntry.Component;

            var list = spawn.Tier switch
            {
                FishGoldCoreSpawnTier.Hostile => _hostile,
                FishGoldCoreSpawnTier.Lesser => _lesser,
                FishGoldCoreSpawnTier.Friendly => _friendly,
                _ => null,
            };

            list?.Add(proto.ID);
        }

        AppendFallback(_hostile, FishSlimeExtractGoldCritPools.LegacyHostilePlasma);
        AppendFallback(_lesser, FishSlimeExtractGoldCritPools.LegacyLesserBlood);
        AppendFallback(_friendly, FishSlimeExtractGoldCritPools.LegacyFriendlyWater);
    }

    private static void AppendFallback(List<string> pool, string[] legacy)
    {
        if (pool.Count > 0)
            return;

        pool.AddRange(legacy);
    }

    public string PickHostile(IRobustRandom random) => random.Pick(_hostile);

    public string PickLesser(IRobustRandom random) => random.Pick(_lesser);

    public string PickFriendly(IRobustRandom random) => random.Pick(_friendly);
}
