using System.Linq;
using System.Numerics;
using Content.Shared.Item;
using Content.Shared._Fish.Psychosis;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.IoC;
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
        SubscribeNetworkEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var (uid, _) in _hallucinations)
            _entityManager.DeleteEntity(uid);
        _hallucinations.Clear();
    }

    private void OnConfig(PsychosisConfigEvent ev)
    {
        _mobPrototypes = ev.MobPrototypes;
        _corpsePrototypes = ev.CorpsePrototypes;
        _itemPrototypes = ev.ItemPrototypes;
        _sounds = ev.Sounds;
    }

    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        if (GetEntity(ev.Uid) != _player.LocalSession?.AttachedEntity)
            return;
        _currentPsychosisLevel = ev.Level;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

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

            var xform = Transform(uid);
            var dist = (xform.WorldPosition - playerXform.WorldPosition).Length();

            // Удаление, если игрок слишком далеко
            if (dist > MaxDistanceFromPlayer)
            {
                StartFadeAndDelete(uid, comp, currentTime);
                continue;
            }

            // Проверка истечения времени жизни
            var elapsed = currentTime - comp.CreatedAt;
            if (elapsed >= comp.Lifetime)
            {
                // Плавно исчезает и удаляется
                StartFadeAndDelete(uid, comp, currentTime);
                continue;
            }

            // Обновление прозрачности в фазе исчезновения
            float remaining = (float)(comp.Lifetime - elapsed).TotalSeconds;
			if (remaining <= FadeDuration)
			{
				float alpha = Math.Clamp(remaining / FadeDuration, 0f, 1f) * comp.StartAlpha;
				if (_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite))
				{
					sprite.Color = sprite.Color.WithAlpha(alpha);
				}
				if (remaining <= 0)
				{
					_entityManager.DeleteEntity(uid);
					_hallucinations.Remove(uid);
					continue;
				}
			}
        }

        // Создание новых галлюцинаций
        if (_currentPsychosisLevel <= 0 ||
            (_mobPrototypes.Count == 0 && _corpsePrototypes.Count == 0 && _itemPrototypes.Count == 0))
            return;

        // Визуальные
        float visualInterval = MathHelper.Lerp(20f, 5f, _currentPsychosisLevel / 100f);
        _nextVisualTime -= frameTime;
        if (_nextVisualTime <= 0)
        {
            TryCreateVisualHallucination(playerEntity.Value);
            _nextVisualTime = visualInterval * _random.NextFloat(0.5f, 1.5f);
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

        // Генерация позиции в пределах видимости (используем вьюпорт)
        var viewport = _eyeManager.GetWorldViewport();
        var min = viewport.BottomLeft;
        var max = viewport.TopRight;

        // Случайная точка в этом прямоугольнике, но не слишком близко к игроку (минимум 2 тайла)
        var playerPos = Transform(player).WorldPosition;
        var randomPos = new Vector2(
            _random.NextFloat(min.X, max.X),
            _random.NextFloat(min.Y, max.Y)
        );
        var offset = randomPos - playerPos;
        if (offset.Length() < 2f)
        {
            var direction = offset.LengthSquared() > 0f ? Vector2.Normalize(offset) : Vector2.UnitX;
            randomPos = playerPos + direction * 2f;
        }

        // Создание сущности
        var hallucination = _entityManager.CreateEntityUninitialized(protoId);
        var xform = _entityManager.GetComponent<TransformComponent>(hallucination);
        xform.WorldPosition = randomPos;

        if (_entityManager.HasComponent<PhysicsComponent>(hallucination))
            _entityManager.RemoveComponent<PhysicsComponent>(hallucination);
        if (_entityManager.HasComponent<ItemComponent>(hallucination))
            _entityManager.RemoveComponent<ItemComponent>(hallucination);

        // Добавляем наш маркер
        var comp = _entityManager.AddComponent<HallucinationComponent>(hallucination);
        comp.CreatedAt = _timing.CurTime;
        comp.Lifetime = TimeSpan.FromSeconds(_random.NextFloat(MinLifetime, MaxLifetime));
        comp.StartAlpha = 1f;

        // Для предметов задаём случайный поворот
        if (!spawnMob)
        {
            var sprite = _entityManager.GetComponent<SpriteComponent>(hallucination);
            sprite.Rotation = _random.NextAngle();
        }

        // Регистрируем в словаре
        _hallucinations[hallucination] = comp;

    }

    private void PlayRandomSound(EntityUid player)
    {
        if (_sounds.Count == 0)
            return;

        var soundPath = _random.Pick(_sounds);
        _audio.PlayGlobal(soundPath, Filter.Local(), false, AudioParams.Default.WithVolume(-6f));
    }

    private void StartFadeAndDelete(EntityUid uid, HallucinationComponent comp, TimeSpan currentTime)
    {
        // Устанавливаем оставшееся время жизни равным FadeDuration
        comp.Lifetime = currentTime - comp.CreatedAt + TimeSpan.FromSeconds(FadeDuration);
        // Если альфа уже была изменена, оставляем, иначе начнём с текущей
    }
}