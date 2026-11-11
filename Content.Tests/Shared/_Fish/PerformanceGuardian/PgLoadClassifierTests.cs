using Content.Shared._Fish.PerformanceGuardian;
using NUnit.Framework;

namespace Content.Tests.Shared._Fish.PerformanceGuardian;

[TestFixture]
[Parallelizable(ParallelScope.All)]
[TestOf(typeof(PgLoadClassifier))]
public sealed class PgLoadClassifierTests
{
    private const float Tick = 1f / 30f;

    private static PgLoadClassifier Create()
    {
        var c = new PgLoadClassifier
        {
            WarmupSamples = 5,
            ConfirmationsRequired = 3,
            PhysicsSpikeThreshold = 2.2f,
            AtmosSpikeThreshold = 2.2f,
            PressureThreshold = 1.55f,
            EventSpikeThreshold = 2.5f,
            PhysicsFloor = 120,
            AtmosFloor = 250,
            EventFloor = 80,
        };
        return c;
    }

    private static bool Feed(PgLoadClassifier c, int awake, int atmos, int events, float frameTime, int times = 1)
    {
        var triggered = false;
        for (var i = 0; i < times; i++)
            triggered |= c.Observe(awake, atmos, events, frameTime, Tick, out _);
        return triggered;
    }

    [Test]
    public void BusyButStableStation_NoFalseIncident()
    {
        var c = Create();
        // Прогрев и длинный «высокий онлайн»: 800 awake, 2000 atmos — стабильно.
        Assert.That(Feed(c, 800, 2000, 25, Tick, times: 40), Is.False);
        Assert.That(c.DescribeState(PgMode.Idle), Is.EqualTo("Норма").Or.EqualTo("Интенсивная игра"));
        Assert.That(c.ClassifyPrimary(800, 2000, 25), Is.EqualTo(PgLoadSource.Ok));
    }

    [Test]
    public void GradualClimbToHighOnline_NoFalseIncident()
    {
        var c = Create();
        for (var i = 0; i < 30; i++)
        {
            var awake = 50 + i * 30;
            var atmos = 100 + i * 60;
            Assert.That(c.Observe(awake, atmos, 10 + i, Tick, Tick, out _), Is.False);
        }
    }

    [Test]
    public void SingleFrameSpike_NoIncident()
    {
        var c = Create();
        Feed(c, 400, 800, 20, Tick, times: 10);
        // Один резкий кадр
        Assert.That(c.Observe(4000, 800, 20, Tick, Tick, out _), Is.False);
        Assert.That(c.ConfirmStreak, Is.EqualTo(1));
    }

    [Test]
    public void SustainedPhysicsSpike_TriggersAndClassifiesPhysics()
    {
        var c = Create();
        Feed(c, 400, 800, 20, Tick, times: 10);
        var triggered = Feed(c, 4000, 800, 20, Tick, times: 3);
        Assert.That(triggered, Is.True);
        Assert.That(c.ClassifyPrimary(4000, 800, 20), Is.EqualTo(PgLoadSource.Physics));
    }

    [Test]
    public void SustainedAtmosSpike_TriggersAndClassifiesAtmos()
    {
        var c = Create();
        Feed(c, 400, 800, 20, Tick, times: 10);
        var triggered = Feed(c, 400, 5000, 20, Tick, times: 3);
        Assert.That(triggered, Is.True);
        Assert.That(c.ClassifyPrimary(400, 5000, 20), Is.EqualTo(PgLoadSource.Atmos));
    }

    [Test]
    public void MassCombatEvents_TriggersEventsWhenSpike()
    {
        var c = Create();
        Feed(c, 400, 800, 30, Tick, times: 10);
        var triggered = Feed(c, 400, 800, 400, Tick, times: 3);
        Assert.That(triggered, Is.True);
        Assert.That(c.ClassifyPrimary(400, 800, 400), Is.EqualTo(PgLoadSource.Events));
    }

    [Test]
    public void RealFrameOverrun_CanTriggerWithoutGaugeSpike()
    {
        var c = Create();
        Feed(c, 400, 800, 20, Tick, times: 10);
        // frameTime >> tick — сервер реально отстаёт
        var triggered = Feed(c, 400, 800, 20, Tick * 2.2f, times: 3);
        Assert.That(triggered, Is.True);
    }

    [Test]
    public void BelowAbsoluteFloors_IgnoresRelativeSpike()
    {
        var c = Create();
        Feed(c, 10, 20, 5, Tick, times: 10);
        // Относительно огромный рост, но абсолюты малы (шум пустой станции)
        Assert.That(Feed(c, 40, 60, 5, Tick, times: 5), Is.False);
    }

    [Test]
    public void WarmupPreventsEarlyIncidents()
    {
        var c = Create();
        c.WarmupSamples = 20;
        // Даже сильный спайк на прогреве не триггерит
        Assert.That(Feed(c, 5000, 8000, 500, Tick * 3f, times: 5), Is.False);
        Assert.That(c.SamplesSeen, Is.LessThanOrEqualTo(20));
    }

    [Test]
    public void Stress_HundredPlayersBusyStation_LongRunFalsePositiveRate()
    {
        var c = Create();
        c.WarmupSamples = 12;
        var rng = new System.Random(42);
        var incidents = 0;

        // Симуляция ~100 игроков: высокий, но относительно стабильный фон + шум ±15%
        for (var i = 0; i < 500; i++)
        {
            var awake = (int)(900 + rng.NextDouble() * 250);
            var atmos = (int)(2500 + rng.NextDouble() * 600);
            var events = (int)(35 + rng.NextDouble() * 25);
            var ft = Tick * (0.95f + (float)rng.NextDouble() * 0.15f);
            if (c.Observe(awake, atmos, events, ft, Tick, out _))
                incidents++;
        }

        // Допустимы единичные ложные на длинной дистанции — цель << 1%
        Assert.That(incidents, Is.LessThanOrEqualTo(2), $"Слишком много ложных инцидентов: {incidents}/500");
    }

    [Test]
    public void Stress_FireExplosionShuttleBurst_Detected()
    {
        var c = Create();
        Feed(c, 500, 1000, 20, Tick, times: 15);

        // Резкий пожар/atmos + всплеск событий (без предварительного «прогрева» на пике)
        Assert.That(Feed(c, 700, 4500, 220, Tick, times: 3), Is.True);
        var src = c.ClassifyPrimary(700, 4500, 220);
        Assert.That(src, Is.EqualTo(PgLoadSource.Atmos).Or.EqualTo(PgLoadSource.Events));
    }

    [Test]
    public void Stress_ClassifierObserve_IsCheap()
    {
        var c = Create();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100_000; i++)
            c.Observe(800, 2000, 40, Tick, Tick, out _);
        // На CI раннерах 100k вызовов могут занимать ~500-600 мс в зависимости от нагрузки хоста.
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000), $"Классификатор слишком тяжёлый: {sw.ElapsedMilliseconds} мс / 100k");
    }
}
