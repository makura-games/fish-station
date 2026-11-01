using Content.Server.Jittering;
using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Применение зелий ксенобиологии на живого слайма.
/// </summary>
public sealed partial class FishSlimePotionSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private JitteringSystem _jitter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSlimeComponent, InteractUsingEvent>(OnSlimeInteractUsing);
        SubscribeLocalEvent<FishSlimeExtractComponent, InteractUsingEvent>(OnExtractInteractUsing);
        SubscribeLocalEvent<ClothingComponent, InteractUsingEvent>(OnClothingInteractUsing);
        SubscribeLocalEvent<ItemComponent, InteractUsingEvent>(OnItemInteractUsing);
        SubscribeLocalEvent<SentienceTargetComponent, InteractUsingEvent>(OnSentienceTargetInteract);
    }

    private void OnSlimeInteractUsing(Entity<FishSlimeComponent> slime, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimePotionComponent>(args.Used, out var potion))
            return;

        if (!_mobState.IsAlive(slime))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-dead"), args.User, args.User);
            return;
        }

        args.Handled = TryApply(slime, potion.Kind, args.User, args.Used);
    }

    private bool TryApply(Entity<FishSlimeComponent> slime, FishSlimePotionKind kind, EntityUid user, EntityUid potion)
    {
        switch (kind)
        {
            case FishSlimePotionKind.Stabilizer:
                return ApplyStabilizer(slime, user, potion);
            case FishSlimePotionKind.Mutator:
                return ApplyMutator(slime, user, potion);
            case FishSlimePotionKind.Steroid:
                return ApplySteroid(slime, user, potion);
            case FishSlimePotionKind.Docility:
                return ApplyDocility(slime, user, potion);
            case FishSlimePotionKind.Speed:
                return ApplySpeed(slime, user, potion);
            case FishSlimePotionKind.Sentience:
                return ApplySentienceOnSlime(slime, user, potion);
            case FishSlimePotionKind.Transference:
                _popup.PopupEntity(Loc.GetString("fish-slime-potion-transference-slime"), user, user);
                return false;
            default:
                return false;
        }
    }

    private bool ApplySpeed(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        if (slime.Comp.AmbientTemperature >= 550f)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-speed-hot"), user, user);
            return false;
        }

        slime.Comp.AmbientTemperature = 550f;
        Dirty(slime);
        _jitter.DoJitter(slime, TimeSpan.FromSeconds(15), refresh: true);
        Finish(slime, user, potion, "fish-slime-potion-speed");
        return true;
    }

    private bool ApplyStabilizer(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        slime.Comp.MutationChance = Math.Max(0, slime.Comp.MutationChance - 15);
        Dirty(slime);
        Finish(slime, user, potion, "fish-slime-potion-stabilizer");
        return true;
    }

    private bool ApplyMutator(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        if (slime.Comp.MutatorUsed)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-mutator-once"), user, user);
            return false;
        }

        if (slime.Comp.MutationChance >= 100)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-mutator-max"), user, user);
            return false;
        }

        slime.Comp.MutationChance = Math.Min(100, slime.Comp.MutationChance + 12);
        slime.Comp.MutatorUsed = true;
        Dirty(slime);
        Finish(slime, user, potion, "fish-slime-potion-mutator");
        return true;
    }

    private bool ApplySteroid(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        if (slime.Comp.IsAdult)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-steroid-baby"), user, user);
            return false;
        }

        if (slime.Comp.Cores >= FishSlimeMutation.MaxCores)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-steroid-max"), user, user);
            return false;
        }

        slime.Comp.Cores++;
        Dirty(slime);
        Finish(slime, user, potion, "fish-slime-potion-steroid");
        return true;
    }

    private bool ApplyDocility(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        if (slime.Comp.Rabid)
        {
            slime.Comp.Rabid = false;
            Dirty(slime);
            Finish(slime, user, potion, "fish-slime-potion-docility-calm");
            return true;
        }

        slime.Comp.Docile = true;
        slime.Comp.Nutrition = 700f;
        var ownerName = _meta.GetEntityName(user);
        _meta.SetEntityName(slime, ownerName);
        Dirty(slime);
        Finish(slime, user, potion, "fish-slime-potion-docility");
        return true;
    }

    private void Finish(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), slime, user);
        if (potion != EntityUid.Invalid)
            QueueDel(potion);
    }
}
