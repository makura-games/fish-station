using Content.Server.Chat.Systems;
using Content.Shared._Fish.Hands;
using Content.Shared.Chat;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;

namespace Content.Server._Fish.Hands;

/// <summary>
///     Передача предметов из рук в руки: Alt + ЛКМ с предметом в руке по другому персонажу протягивает ему предмет,
///     Alt + ЛКМ по протягивающему забирает предмет.
/// </summary>
public sealed class HandOfferSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ChatSystem _chat = default!;

    // Выше остальных альтернативных действий, чтобы Alt + ЛКМ запускал именно передачу.
    private const int VerbPriority = 10;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<HandsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        var user = args.User;
        var target = ent.Owner;

        // Получатель забирает предмет у того, кто его протягивает.
        if (TryComp<HandOfferComponent>(target, out var offer) &&
            offer.Target == user &&
            _hands.IsHolding(target, offer.Item) &&
            _hands.CanPickupAnyHand(user, offer.Item))
        {
            var item = offer.Item;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("hand-offer-take-verb", ("item", item)),
                Priority = VerbPriority,
                Act = () => TryTake(user, target, item),
            });
            return;
        }

        // Протянуть предмет, который держим в активной руке.
        if (args.Using is { } used &&
            _hands.IsHolding(user, used) &&
            !_mobState.IsIncapacitated(target))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("hand-offer-give-verb", ("item", used)),
                Priority = VerbPriority,
                Act = () => Offer(user, target, used),
            });
        }
    }

    private void Offer(EntityUid user, EntityUid target, EntityUid item)
    {
        if (!_hands.IsHolding(user, item) || _mobState.IsIncapacitated(target))
            return;

        var offer = EnsureComp<HandOfferComponent>(user);
        offer.Item = item;
        offer.Target = target;

        _chat.TrySendInGameICMessage(user,
            Loc.GetString("hand-offer-give-emote", ("item", item), ("target", target)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal);
    }

    private void TryTake(EntityUid user, EntityUid giver, EntityUid item)
    {
        if (!TryComp<HandOfferComponent>(giver, out var offer) ||
            offer.Target != user ||
            offer.Item != item ||
            !_hands.IsHolding(giver, item) ||
            !_hands.CanPickupAnyHand(user, item))
            return;

        // Предмет уходит из рук отдающего без выпадения на пол и сразу берётся получателем.
        if (!_hands.TryDrop(giver, item, checkActionBlocker: false, doDropInteraction: false))
            return;

        RemComp<HandOfferComponent>(giver);

        if (!_hands.TryPickupAnyHand(user, item))
        {
            // Руки получателя не приняли предмет — возвращаем его владельцу.
            _hands.TryPickupAnyHand(giver, item, checkActionBlocker: false);
            return;
        }

        _chat.TrySendInGameICMessage(user,
            Loc.GetString("hand-offer-take-emote", ("item", item), ("giver", giver)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal);
    }
}
