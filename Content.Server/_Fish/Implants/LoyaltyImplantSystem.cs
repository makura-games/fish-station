using Content.Shared.Implants.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Server.Chat.Managers;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Content.Shared.Implants;
using Content.Shared.Interaction.Events;
using Content.Server.NPC.Systems;
using Content.Shared.Mindshield.Components;

namespace Content.Server.Implants;

public sealed partial class LoyaltyImplantSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoyaltyImplantComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LoyaltyImplantComponent, ImplantImplantedEvent>(OnImplanted);
    }

    private void OnInit(Entity<LoyaltyImplantComponent> ent, ref ComponentInit args)
    {
        // Don't initialize NextMessageTime here - it's runtime state
        // Will be set when implant is actually implanted in an entity
    }

    private void OnImplanted(Entity<LoyaltyImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        var implanted = args.Implanted;

        // Initialize NextMessageTime when implant is actually implanted
        ent.Comp.NextMessageTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);

        if (!TryComp<ActorComponent>(implanted, out var actor))
            return;

        var message = Loc.GetString("loyalty-implant-initial-message");

        _popup.PopupEntity(message, implanted, implanted, PopupType.Large);
        _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, default, false, actor.PlayerSession.Channel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LoyaltyImplantComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var uid, out var loyalty, out var subdermal))
        {
            if (_timing.CurTime < loyalty.NextMessageTime)
                continue;

            loyalty.NextMessageTime = _timing.CurTime + TimeSpan.FromSeconds(loyalty.Interval);

            if (subdermal.ImplantedEntity is not { } implanted)
                continue;

            if (!TryComp<ActorComponent>(implanted, out var actor))
                continue;

            var messageIndex = _random.Next(1, 11);
            var message = Loc.GetString($"loyalty-implant-message-{messageIndex}");

            _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, default, false, actor.PlayerSession.Channel);
        }
    }
}
