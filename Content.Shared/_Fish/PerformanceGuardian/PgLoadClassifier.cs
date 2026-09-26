namespace Content.Shared._Fish.PerformanceGuardian;

/// <summary>
/// Чистая логика «норма / интенсивно / аномалия» без ECS — удобно юнит-тестировать.
/// </summary>
public sealed class PgLoadClassifier
{
    public float PhysicsSpikeThreshold { get; set; } = 2.2f;
    public float AtmosSpikeThreshold { get; set; } = 2.2f;
    public float PressureThreshold { get; set; } = 1.55f;
    public float EventSpikeThreshold { get; set; } = 2.5f;

    /// <summary>Минимум awake, ниже которого physics-spike игнорируется.</summary>
    public int PhysicsFloor { get; set; } = 120;

    /// <summary>Минимум atmos active tiles для atmos-spike.</summary>
    public int AtmosFloor { get; set; } = 250;

    /// <summary>Минимум событий/с для event-spike.</summary>
    public int EventFloor { get; set; } = 80;

    /// <summary>Сколько подряд аномальных сэмплов нужно для авто-инцидента.</summary>
    public int ConfirmationsRequired { get; set; } = 3;

    /// <summary>Сэмплов прогрева baseline до авто-инцидентов.</summary>
    public int WarmupSamples { get; set; } = 12;

    private float _baseAwake = 1f;
    private float _baseAtmos = 1f;
    private float _baseEvents = 1f;
    private float _basePressure = 1f;
    private int _samples;
    private int _confirmStreak;

    public int SamplesSeen => _samples;
    public int ConfirmStreak => _confirmStreak;
    public float BaseAwake => _baseAwake;
    public float BaseAtmos => _baseAtmos;
    public float BaseEvents => _baseEvents;
    public float BasePressure => _basePressure;

    public float AwakeSpike { get; private set; } = 1f;
    public float AtmosSpike { get; private set; } = 1f;
    public float EventSpike { get; private set; } = 1f;
    public float PressureRatio { get; private set; } = 1f;

    public void Reset()
    {
        _baseAwake = 1f;
        _baseAtmos = 1f;
        _baseEvents = 1f;
        _basePressure = 1f;
        _samples = 0;
        _confirmStreak = 0;
        AwakeSpike = AtmosSpike = EventSpike = PressureRatio = 1f;
    }

    /// <summary>
    /// Обновить baseline и метрики. Возвращает true, если пора запускать авто-диагностику.
    /// </summary>
    public bool Observe(
        int awakeBodies,
        int atmosActive,
        int eventRatePerSec,
        float frameTimeSeconds,
        float tickPeriodSeconds,
        out PgLoadSource primaryHint)
    {
        _samples++;

        var tickPeriod = Math.Max(0.001f, tickPeriodSeconds);
        PressureRatio = Math.Clamp(frameTimeSeconds / tickPeriod, 0f, 8f);

        // На прогреве baseline растёт быстрее — меньше ложных всплесков в начале раунда.
        var alpha = _samples <= WarmupSamples ? 0.35f : 0.08f;
        _baseAwake = Lerp(_baseAwake, Math.Max(1f, awakeBodies), alpha);
        _baseAtmos = Lerp(_baseAtmos, Math.Max(1f, atmosActive), alpha);
        _baseEvents = Lerp(_baseEvents, Math.Max(1f, eventRatePerSec), alpha);
        _basePressure = Lerp(_basePressure, Math.Max(0.5f, PressureRatio), alpha);

        AwakeSpike = awakeBodies / Math.Max(1f, _baseAwake);
        AtmosSpike = atmosActive / Math.Max(1f, _baseAtmos);
        EventSpike = eventRatePerSec / Math.Max(1f, _baseEvents);

        var anomalous = IsAnomalous(awakeBodies, atmosActive, eventRatePerSec);
        primaryHint = ClassifyPrimary(awakeBodies, atmosActive, eventRatePerSec, anomalous);

        if (!anomalous)
        {
            _confirmStreak = 0;
            return false;
        }

        _confirmStreak++;
        if (_samples <= WarmupSamples)
            return false;

        return _confirmStreak >= ConfirmationsRequired;
    }

    public bool IsAnomalous(int awakeBodies, int atmosActive, int eventRatePerSec)
    {
        // Интенсивная, но стабильная игра (высокий baseline) — не аномалия.
        var physicsHit = awakeBodies >= PhysicsFloor
                         && AwakeSpike >= PhysicsSpikeThreshold;
        var atmosHit = atmosActive >= AtmosFloor
                       && AtmosSpike >= AtmosSpikeThreshold;
        var eventHit = eventRatePerSec >= EventFloor
                       && EventSpike >= EventSpikeThreshold;
        // Реальный overrun тика (frameTime), не выдуманный из atmos/physics.
        var pressureHit = PressureRatio >= PressureThreshold
                          && PressureRatio >= _basePressure * 1.25f;

        // Нужен хотя бы один «жёсткий» сигнал; одиночный мягкий pressure без содержимого — нет.
        if (physicsHit || atmosHit || eventHit)
            return true;

        // Pressure сам по себе — только если сильно и устойчиво выше своего baseline.
        return pressureHit && PressureRatio >= PressureThreshold * 1.15f;
    }

    public PgLoadSource ClassifyPrimary(int awakeBodies, int atmosActive, int eventRatePerSec, bool? isAnomalous = null)
    {
        var anomalous = isAnomalous ?? IsAnomalous(awakeBodies, atmosActive, eventRatePerSec);
        if (!anomalous
            && PressureRatio < 1.25f
            && AwakeSpike < 1.4f
            && AtmosSpike < 1.4f
            && EventSpike < 1.4f)
            return PgLoadSource.Ok;

        var physicsScore = awakeBodies >= PhysicsFloor ? AwakeSpike : 0f;
        var atmosScore = atmosActive >= AtmosFloor ? AtmosSpike : 0f;
        var eventScore = eventRatePerSec >= EventFloor / 2 ? EventSpike : 0f;
        var entityScore = 0f; // заполняется снаружи при огромном EntityCount

        var best = PgLoadSource.Ok;
        var bestScore = 1.2f;

        if (physicsScore > bestScore)
        {
            best = PgLoadSource.Physics;
            bestScore = physicsScore;
        }

        if (atmosScore > bestScore)
        {
            best = PgLoadSource.Atmos;
            bestScore = atmosScore;
        }

        if (eventScore > bestScore)
        {
            best = PgLoadSource.Events;
            bestScore = eventScore;
        }

        if (entityScore > bestScore)
            best = PgLoadSource.Entities;

        if (best == PgLoadSource.Ok && PressureRatio >= 1.35f)
        {
            // Есть overrun, но относительные спайки слабые — укажем наиболее вероятное.
            if (atmosScore >= physicsScore && atmosScore >= eventScore && atmosActive >= AtmosFloor)
                return PgLoadSource.Atmos;
            if (physicsScore >= eventScore && awakeBodies >= PhysicsFloor)
                return PgLoadSource.Physics;
            if (eventRatePerSec >= EventFloor / 2)
                return PgLoadSource.Events;
        }

        return best;
    }

    public string DescribeState(PgMode mode)
    {
        if (mode == PgMode.Incident)
            return "Инцидент — идёт разбор нагрузки";

        if (PressureRatio >= PressureThreshold || AwakeSpike >= PhysicsSpikeThreshold || AtmosSpike >= AtmosSpikeThreshold)
            return "Высокая нагрузка";

        if (PressureRatio >= 1.25f || AwakeSpike >= 1.5f || AtmosSpike >= 1.5f)
            return "Интенсивная игра";

        return "Норма";
    }

    public static string SourceToRu(PgLoadSource source) => source switch
    {
        PgLoadSource.Physics => "Физика (много движущихся объектов)",
        PgLoadSource.Atmos => "Атмосфера (активные тайлы / пожары)",
        PgLoadSource.Events => "Игровые события (бои, взрывы, броски)",
        PgLoadSource.Entities => "Слишком много сущностей на сервере",
        PgLoadSource.Ok => "Явной перегрузки нет",
        _ => "Неизвестно",
    };

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
