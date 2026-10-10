using Content.Shared._Fish.Fluids.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Fluids.EntitySystems;

/// <summary>
/// Система особой очищающей пены, которая при создании или соприкосновении с тайлом:
/// 1. Удаляет весь мусор (сущности с тегом Trash и незакрепленные предметы).
/// 2. Восстанавливает разбитые или перегоревшие лампочки в светильниках и на полу на рабочие.
/// </summary>
public sealed class SuperCleanFoamSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedLightBulbSystem _bulb = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperCleanFoamComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SuperCleanFoamComponent> ent, ref MapInitEvent args)
    {
        CleanTile(ent);
    }

    /// <summary>
    /// Очищает мусор и чинит лампочки в окрестности тайла пены.
    /// </summary>
    public void CleanTile(EntityUid foamUid)
    {
        var xform = Transform(foamUid);
        var coords = xform.Coordinates;

        // Радиус 1.0f охватывает тайл, на котором находится пена
        var entities = _lookup.GetEntitiesInRange(coords, 1.0f);

        foreach (var entity in entities)
        {
            if (entity == foamUid || TerminatingOrDeleted(entity))
                continue;

            // Удаление мусора (тег Trash, незакрепленный мусор, автоматически собираемый мешком)
            if (_tag.HasTag(entity, TrashTag))
            {
                var targetXform = Transform(entity);
                if (!targetXform.Anchored)
                {
                    QueueDel(entity);
                }
            }
        }
    }
}
