using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.CollectiveMind
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)] // Fish-edit: синхронизируем список коллективных разумов с клиентом
    public sealed partial class CollectiveMindComponent : Component
    {
        [DataField, AutoNetworkedField] // Fish-edit: передаём список разумов клиенту для обновления прав чата
        public List<ProtoId<CollectiveMindPrototype>> Minds = [];
    }
}
