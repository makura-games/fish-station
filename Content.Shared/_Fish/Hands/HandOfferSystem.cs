using Content.Shared.Chat;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;

namespace Content.Shared._Fish.Hands;

/// <summary>
///     Передача предметов из рук в руки: Alt + ЛКМ с предметом в руке по другому персонажу протягивает ему предмет,
///     Alt + ЛКМ по протягивающему забирает предмет.
/// </summary>
public sealed partial class HandOfferSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedChatSystem _chat = default!;

    /// <summary>
    ///     Приоритет действий передачи: выше остальных Alt-действий, чтобы Alt + ЛКМ запускал именно передачу.
    /// </summary>
    public const int VerbPriority = 10;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HandOfferComponent, DidUnequipHandEvent>(OnUnequipped);
    }

    /// <summary>
    ///     Добавляет Alt-действие «протянуть» или «взять» в зависимости от того, кто кликает и что у него в руках.
    /// </summary>
    private void OnGetVerbs(Entity<HandsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        var user = args.User;
        var target = ent.Owner;

        // Получатель забирает предмет у того, кто его протягивает.
        if (CanTake(user, target, out var offered))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("hand-offer-take-verb", ("item", offered)),
                Priority = VerbPriority,
                Act = () => TryTake(user, target),
            });
            return;
        }

        // Протянуть предмет, который держим в активной руке.
        if (args.Using is { } used && CanOffer(user, target, used))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("hand-offer-give-verb", ("item", used)),
                Priority = VerbPriority,
                Act = () => TryOffer(user, target, used),
            });
        }
    }

    /// <summary>
    ///     Сбрасывает предложение, когда протягиваемый предмет покидает руки.
    /// </summary>
    private void OnUnequipped(Entity<HandOfferComponent> ent, ref DidUnequipHandEvent args)
    {
        if (args.Unequipped == ent.Comp.Item)
            RemComp<HandOfferComponent>(ent);
    }

    /// <summary>
    ///     Можно ли <paramref name="user"/> протянуть <paramref name="item"/> персонажу <paramref name="target"/>.
    /// </summary>
    public bool CanOffer(EntityUid user, EntityUid target, EntityUid item)
    {
        return _hands.IsHolding(user, item) && !_mobState.IsIncapacitated(target);
    }

    /// <summary>
    ///     Протягивает предмет, если это разрешено проверкой <see cref="CanOffer"/>.
    /// </summary>
    public bool TryOffer(EntityUid user, EntityUid target, EntityUid item)
    {
        if (!CanOffer(user, target, item))
            return false;

        DoOffer(user, target, item);
        return true;
    }

    /// <summary>
    ///     Запоминает предложение и сообщает о нём эмоутом.
    /// </summary>
    public void DoOffer(EntityUid user, EntityUid target, EntityUid item)
    {
        var offer = EnsureComp<HandOfferComponent>(user);
        offer.Item = item;
        offer.Target = target;
        Dirty(user, offer);

        _chat.TrySendInGameICMessage(user,
            Loc.GetString("hand-offer-give-emote",
                ("item", item),
                ("target", Identity.Entity(target, EntityManager))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal);
    }

    /// <summary>
    ///     Можно ли <paramref name="user"/> забрать предмет, который протягивает <paramref name="giver"/>.
    /// </summary>
    public bool CanTake(EntityUid user, EntityUid giver, out EntityUid item)
    {
        item = default;

        if (!TryComp<HandOfferComponent>(giver, out var offer) ||
            offer.Target != user ||
            !_hands.IsHolding(giver, offer.Item) ||
            !_hands.CanPickupAnyHand(user, offer.Item))
            return false;

        item = offer.Item;
        return true;
    }

    /// <summary>
    ///     Забирает протянутый предмет, если это разрешено проверкой <see cref="CanTake"/>.
    /// </summary>
    public bool TryTake(EntityUid user, EntityUid giver)
    {
        if (!CanTake(user, giver, out var item))
            return false;

        // Отдающий должен иметь право выпустить предмет из рук.
        if (!_hands.TryDrop(giver, item, doDropInteraction: false))
            return false;

        if (!_hands.TryPickupAnyHand(user, item))
        {
            // Руки получателя не приняли предмет — возвращаем его владельцу.
            _hands.TryPickupAnyHand(giver, item, checkActionBlocker: false);
            return false;
        }

        DoTake(user, giver, item);
        return true;
    }

    /// <summary>
    ///     Сообщает эмоутом, что предмет забрали.
    /// </summary>
    public void DoTake(EntityUid user, EntityUid giver, EntityUid item)
    {
        _chat.TrySendInGameICMessage(user,
            Loc.GetString("hand-offer-take-emote",
                ("item", item),
                ("giver", Identity.Entity(giver, EntityManager))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal);
    }
}
