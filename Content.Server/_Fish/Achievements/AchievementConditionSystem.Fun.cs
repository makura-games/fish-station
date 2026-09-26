using Content.Shared._Fish.Achievements;
using Content.Shared._Fish.Achievements.Events;
using Content.Shared.Maps;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Achievements;

public sealed partial class AchievementConditionSystem
{
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    partial void InitializeFun()
    {
        SubscribeLocalEvent<FishTilePriedEvent>(OnFishTilePried);
    }

    partial void ClearFunRoundState()
    {
    }

    private void OnFishTilePried(ref FishTilePriedEvent args)
    {
        if (!TryComp<ToolComponent>(args.Tool, out var tool) || !_tools.HasQuality(args.Tool, PryingQuality, tool))
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var tileDef = (ContentTileDefinition)_tileDefinitionManager[args.RemovedTileId];
        if (!IsIntactFloorTile(tileDef))
            return;

        _ = _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.TilePry,
            new AchievementTriggerContext(
                EntityPrototypeId: GetPrototypeId(args.Tool),
                VerifiedTag: "IntactFloor",
                EventKey: $"tile-pry:{GetNetEntity(args.Grid)}:{args.Tile}:{actor.PlayerSession.UserId}"));
    }

    /// <summary>Исключает повреждённые/сгоревшие варианты (см. FTL Misclick).</summary>
    private static bool IsIntactFloorTile(ContentTileDefinition tileDef)
    {
        if (!tileDef.CanCrowbar)
            return false;

        var id = tileDef.ID;
        return !id.Contains("Damaged", StringComparison.OrdinalIgnoreCase)
               && !id.Contains("Burnt", StringComparison.OrdinalIgnoreCase);
    }
}
