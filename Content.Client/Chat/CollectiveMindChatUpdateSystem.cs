using Content.Client.Chat.Managers;
using Content.Shared._Sunrise.CollectiveMind;
using Robust.Client.Player;

namespace Content.Client.Chat
{
    public sealed partial class CollectiveMindChatUpdateSystem : EntitySystem
    {
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CollectiveMindComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<CollectiveMindComponent, ComponentRemove>(OnRemove);
            SubscribeLocalEvent<CollectiveMindComponent, AfterAutoHandleStateEvent>(OnHandleState); // Fish-edit: обновляем права чата при синхронизации списка коллективных разумов
        }

        public CollectiveMindComponent? Player => CompOrNull<CollectiveMindComponent>(_playerManager.LocalSession?.AttachedEntity);
        public bool IsCollectiveMind => Player != null;

        private void OnInit(EntityUid uid, CollectiveMindComponent component, ComponentInit args)
        {
            _chatManager.UpdatePermissions();
        }

        private void OnRemove(EntityUid uid, CollectiveMindComponent component, ComponentRemove args)
        {
            _chatManager.UpdatePermissions();
        }

        // Fish-start: обновление прав чата при изменении состояния CollectiveMindComponent
        private void OnHandleState(Entity<CollectiveMindComponent> ent, ref AfterAutoHandleStateEvent args)
        {
            _chatManager.UpdatePermissions();
        }
        // Fish-end
    }
}
