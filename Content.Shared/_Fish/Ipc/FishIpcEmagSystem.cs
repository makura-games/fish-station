using Content.Shared.Access.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Дешифратор на КПБ сбрасывает списки доступа шасси. Попап объясняет этот эффект.
/// Заряд не тратится повторно: это делает <see cref="AccessReaderComponent"/>.
/// </summary>
public sealed partial class FishIpcEmagSystem : EntitySystem
{
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<FishIpcComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Access))
            return;

        if (!TryComp<AccessReaderComponent>(ent, out var reader))
            return;

        // Уже пусто и событие никто не обработал — заряд не тратим и ничего не обещаем.
        if (reader.AccessLists.Count == 0 && !args.Handled)
            return;

        _popup.PopupEntity(Loc.GetString("fish-ipc-emag-access"), ent, args.UserUid);
    }
}
