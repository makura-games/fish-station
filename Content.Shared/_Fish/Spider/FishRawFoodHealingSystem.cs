using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Spider;

/// <summary>
/// Перерабатывает сырые животные белки в усвояемый белок и начисляет лечебный реагент
/// и сытость существам с <see cref="FishRawFoodHealingComponent"/> при съедании сырой пищи или органов.
/// </summary>
public sealed partial class FishRawFoodHealingSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private TagSystem _tag = default!;

    [Dependency] private EntityQuery<EdibleComponent> _edibleQuery = default!;
    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;
    [Dependency] private EntityQuery<SatiationComponent> _satiationQuery = default!;

    private static readonly ProtoId<TagPrototype> RawTag = "Raw";
    private static readonly ProtoId<ReagentPrototype> UncookedProteinsReagent = "UncookedAnimalProteins";
    private static readonly ProtoId<ReagentPrototype> ProteinReagent = "Protein";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishRawFoodHealingComponent, IngestingEvent>(OnIngesting);
    }

    private void OnIngesting(Entity<FishRawFoodHealingComponent> ent, ref IngestingEvent args)
    {
        var rawProteins = args.Split.GetTotalPrototypeQuantity(UncookedProteinsReagent);
        var isRawOrOrgan = IsRawFoodOrOrgan(args.Food, rawProteins);
        if (!isRawOrOrgan)
            return;

        // Переводим сырые белки в обычный Protein до попадания в желудок,
        // чтобы органы и сырое мясо нормально переваривались на стадии Digestion и утоляли голод.
        if (rawProteins > FixedPoint2.Zero)
        {
            args.Split.RemoveReagent(UncookedProteinsReagent, rawProteins);
            args.Split.AddReagent(ProteinReagent, rawProteins);
        }

        // Начисляем ихор и мгновенный бонус сытости один раз за полностью съеденный кусок мяса или орган.
        if (!_edibleQuery.TryComp(args.Food, out var edible) || !_ingestion.IsEmpty((args.Food, edible)))
            return;

        var healSolution = new Solution(ent.Comp.HealReagent, ent.Comp.HealAmount);
        _bloodstream.TryAddToBloodstream(ent.Owner, healSolution);

        if (ent.Comp.HungerBonus > 0f && _satiationQuery.TryComp(ent, out var satiation))
            _satiation.ModifyValue((ent, satiation), SatiationSystem.Hunger, ent.Comp.HungerBonus);
    }

    private bool IsRawFoodOrOrgan(EntityUid food, FixedPoint2 rawProteins)
    {
        if (rawProteins > FixedPoint2.Zero)
            return true;

        if (_organQuery.HasComp(food))
            return true;

        return _tag.HasTag(food, RawTag);
    }
}
