using System.Linq;
using System.Numerics;
using Content.Shared.Item;
using Content.Shared.Dataset;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Localization;
using Content.Shared._Fish.Psychosis;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Psychosis;

public sealed partial class HallucinationClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<LocalizedDatasetPrototype> FirstNames = "NamesFirstMale";
    private static readonly ProtoId<LocalizedDatasetPrototype> LastNames = "NamesLast";

    private readonly HashSet<EntityUid> _intersecting = new();
    private const int PositionAttempts = 20;
    private const float SpawnClearance = 0.5f;

    private List<string> _mobPrototypes = new();
    private List<string> _corpsePrototypes = new();
    private List<string> _itemPrototypes = new();
    private List<string> _sounds = new();

    private float _currentPsychosisLevel;
    private float _nextVisualTime;
    private float _nextSoundTime;
    private const float MinLifetime = 10f;
    private const float MaxLifetime = 20f;
    private const float FadeDuration = 3f; // сколько секунд в конце исчезает
    private const float MaxDistanceFromPlayer = 25f; // удаление при превышении

    private readonly Dictionary<EntityUid, HallucinationComponent> _hallucinations = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PsychosisConfigEvent>(OnConfig);
        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    /// <summary>
    /// Удаляет созданные клиентом сущности галлюцинаций при завершении системы.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();
        ClearHallucinations();
    }

    /// <summary>
    /// Сохраняет полученные от сервера списки прототипов и звуков для галлюцинаций.
    /// </summary>
    private void OnConfig(PsychosisConfigEvent ev)
    {
        _mobPrototypes = ev.MobPrototypes;
        _corpsePrototypes = ev.CorpsePrototypes;
        _itemPrototypes = ev.ItemPrototypes;
        _sounds = ev.Sounds;
    }

    /// <summary>
    /// Обновляет уровень психоза, только если событие относится к локальному игроку.
    /// </summary>
    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        _currentPsychosisLevel = ev.Level;
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _currentPsychosisLevel = 0f;
        ClearHallucinations();
    }

    private void ClearHallucinations()
    {
        foreach (var uid in _hallucinations.Keys)
            _entityManager.DeleteEntity(uid);

        _hallucinations.Clear();
    }

    /// <summary>
    /// Обновляет время жизни галлюцинаций и создаёт новые визуальные и звуковые эффекты.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        // Обновление существующих галлюцинаций: плавное исчезновение, удаление по таймеру/дистанции
        var playerEntity = _player.LocalSession?.AttachedEntity;
        if (playerEntity == null)
            return;

        var playerXform = Transform(playerEntity.Value);
        var currentTime = _timing.CurTime;

        foreach (var (uid, comp) in _hallucinations.ToArray())
        {
            if (!_entityManager.EntityExists(uid))
            {
                _hallucinations.Remove(uid);
                continue;
            }

            if (comp.FadeStartedAt is { } fadeStartedAt)
            {
                var fadeElapsed = currentTime - fadeStartedAt;
                var alpha = Math.Clamp(1f - (float) fadeElapsed.TotalSeconds / FadeDuration, 0f, 1f) *
                            comp.StartAlpha;
                if (_entityManager.TryGetComponent<SpriteComponent>(uid, out var fadeSprite))
                    fadeSprite.Color = fadeSprite.Color.WithAlpha(alpha);

                if (fadeElapsed >= TimeSpan.FromSeconds(FadeDuration))
                {
                    _entityManager.DeleteEntity(uid);
                    _hallucinations.Remove(uid);
                }

                continue;
            }

            var xform = Transform(uid);
            var dist = (xform.WorldPosition - playerXform.WorldPosition).Length();

            // Удаление, если игрок слишком далеко
            if (dist > MaxDistanceFromPlayer)
            {
                StartFadeAndDelete(comp, currentTime);
                continue;
            }

            // Проверка истечения времени жизни
            var elapsed = currentTime - comp.CreatedAt;
            if (elapsed >= comp.Lifetime)
            {
                StartFadeAndDelete(comp, currentTime);
                continue;
            }
        }

        if (_currentPsychosisLevel <= 0)
            return;

        if (_mobPrototypes.Count > 0 || _corpsePrototypes.Count > 0 || _itemPrototypes.Count > 0)
        {
            float visualInterval = MathHelper.Lerp(20f, 5f, _currentPsychosisLevel / 100f);
            _nextVisualTime -= frameTime;
            if (_nextVisualTime <= 0)
            {
                TryCreateVisualHallucination(playerEntity.Value);
                _nextVisualTime = visualInterval * _random.NextFloat(0.5f, 1.5f);
            }
        }

        // Звуковые
        float soundInterval = MathHelper.Lerp(30f, 5f, _currentPsychosisLevel / 100f);
        _nextSoundTime -= frameTime;
        if (_nextSoundTime <= 0)
        {
            PlayRandomSound(playerEntity.Value);
            _nextSoundTime = soundInterval * _random.NextFloat(0.5f, 1.5f);
        }
    }

    /// <summary>
    /// Создаёт случайную визуальную галлюцинацию в пределах видимой области игрока.
    /// </summary>
    private void TryCreateVisualHallucination(EntityUid player)
    {
		// Выбор категории
        var roll = _random.NextFloat();
        string? protoId = null;
        var spawnMob = false;

        if (roll < 0.4f && _mobPrototypes.Count > 0)
        {
            protoId = _random.Pick(_mobPrototypes);
            spawnMob = true;
        }
        else if (roll < 0.7f && _corpsePrototypes.Count > 0)
            protoId = _random.Pick(_corpsePrototypes);
        else if (_itemPrototypes.Count > 0)
            protoId = _random.Pick(_itemPrototypes);
        else if (_mobPrototypes.Count > 0)
        {
            protoId = _random.Pick(_mobPrototypes);
            spawnMob = true;
        }

		if (protoId == null)
			return;

        if (!TryFindSpawnPosition(player, out var position))
            return;

        var hallucination = _entityManager.CreateEntityUninitialized(protoId, position);

        // Добавляем наш маркер
        var comp = _entityManager.AddComponent<HallucinationComponent>(hallucination);
        comp.CreatedAt = _timing.CurTime;
        comp.Lifetime = TimeSpan.FromSeconds(_random.NextFloat(MinLifetime, MaxLifetime));
        comp.StartAlpha = 1f;

        // Fish-edit: визуализаторы предметов должны получить компоненты исходного прототипа при запуске.
        _entityManager.InitializeAndStartEntity(hallucination);

        if (TryComp<PhysicsComponent>(hallucination, out var physics))
            _physics.SetCanCollide(hallucination, false, body: physics);
        RemComp<ItemComponent>(hallucination);

        if (TryComp<SpriteComponent>(hallucination, out var sprite))
        {
            _sprite.SetVisible((hallucination, sprite), true);
            if (!spawnMob)
                _sprite.SetRotation((hallucination, sprite), _random.NextAngle());
        }

        if (spawnMob)
        {
            var firstNames = _prototype.Index(FirstNames);
            var lastNames = _prototype.Index(LastNames);
            var name = $"{Loc.GetString(_random.Pick(firstNames.Values))} {Loc.GetString(_random.Pick(lastNames.Values))}";
            _metaData.SetEntityName(hallucination, name);
        }

        // Регистрируем в словаре
        _hallucinations[hallucination] = comp;

    }

    /// <summary>
    /// Ищет свободное место с запасом до стен для любой категории галлюцинаций.
    /// </summary>
    private bool TryFindSpawnPosition(EntityUid player, out MapCoordinates position)
    {
        position = default;
        var viewport = _eyeManager.GetWorldViewport();
        var xform = Transform(player);
        var playerPosition = _transform.GetWorldPosition(xform);
        for (var attempt = 0; attempt < PositionAttempts; attempt++)
        {
            var candidate = new Vector2(
                _random.NextFloat(viewport.BottomLeft.X, viewport.TopRight.X),
                _random.NextFloat(viewport.BottomLeft.Y, viewport.TopRight.Y));
            if (Vector2.DistanceSquared(candidate, playerPosition) < 4f)
                continue;

            var bounds = new Box2(candidate - new Vector2(SpawnClearance), candidate + new Vector2(SpawnClearance));
            _intersecting.Clear();
            _lookup.GetEntitiesIntersecting(xform.MapID, bounds, _intersecting, LookupFlags.Uncontained);
            if (!CanSpawnAtPosition())
                continue;

            position = new MapCoordinates(candidate, xform.MapID);
            return true;
        }

        return false;
    }

    private bool CanSpawnAtPosition()
    {
        foreach (var uid in _intersecting)
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics) || !physics.CanCollide)
                continue;

            if (!TryComp<FixturesComponent>(uid, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & (int) CollisionGroup.MobMask) != 0)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проигрывает случайный звук галлюцинации локальному игроку.
    /// </summary>
    private void PlayRandomSound(EntityUid player)
    {
        if (_sounds.Count == 0)
            return;

        var soundPath = _random.Pick(_sounds);
        _audio.PlayGlobal(soundPath, Filter.Local(), false, AudioParams.Default.WithVolume(-6f));
    }

    /// <summary>
    /// Сокращает оставшееся время жизни галлюцинации для её плавного исчезновения.
    /// </summary>
    private void StartFadeAndDelete(HallucinationComponent comp, TimeSpan currentTime)
    {
        comp.FadeStartedAt ??= currentTime;
    }
}