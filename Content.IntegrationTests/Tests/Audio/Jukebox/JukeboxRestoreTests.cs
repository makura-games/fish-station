#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Audio.Jukebox;
using Content.Shared.Audio.Jukebox;
using Content.Shared._Sunrise.TapePlayer;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Audio.Jukebox;

/// <summary>
/// Ensures the upstream SS14 jukebox catalog and machine wiring remain restored
/// (not replaced by Sunrise TapePlayer on the station jukebox).
/// </summary>
public sealed class JukeboxRestoreTests : GameTest
{
    /// <summary>Original upstream SS14 jukebox tracks.</summary>
    private static readonly string[] UpstreamTracks =
    [
        "FlipFlap",
        "Tintin",
        "Thunderdome",
        "Constellations",
        "Drifting",
        "starlight",
    ];

    /// <summary>Licensed classic SS13 title/lobby + Fish tracks added to the jukebox catalog.</summary>
    private static readonly string[] ClassicSs13AndFishTracks =
    [
        "FishEndlessSpace",
        "FishAbsconditus",
        "FishRobocopTitle",
        "FishAtomicAmnesia",
        "FishSinguloose",
        "FishCometHaley",
        "FishSpacStac",
        "FishPhoronWillMakeUsRich",
        "FishLasersRipApartTheBulkhead",
        "FishEveryLightIsBlinkingAtOnce",
        "FishSpaceAsshole",
        "FishTheWizard",
        "FishSunset",
        "FishNeon",
        "FishDoubleU",
    ];

    /// <summary>Explicit SS13/SS14 community soundtrack artists already licensed in-repo.</summary>
    private static readonly string[] CommunitySsSoundtrackTracks =
    [
        "FishBolgarichBurnTheHive",
        "FishBolgarichDestinyOfColonists",
        "FishBolgarichDestroyed",
        "FishBolgarichHikingInTheFog",
        "FishBolgarichTangoWithBloodshed",
        "FishBolgarichTearOfVeil",
        "FishBolgarichTheirFinalShift",
        "FishBolgarichVendetta",
        "FishBolgarichRunningOut",
        "FishCountdown",
        "FishSoundStation14",
        "FishBlatantlyNuclear",
        "FishClearlyNuclear",
        "FishNukeMass",
        "FishLastCall",
        "FishSponsoredBySyndicate",
        "FishNuclearOpsFull",
        "FishNuclearOpsPrep",
        "FishNuclearOpsInfil",
        "FishNuclearOpsDef",
        "FishRevolutionTheme",
        "FishAGuyNuclearOperationsOriginal",
        "FishAGuyChaosTheory",
        "FishAGuyMainframeBitflip",
        "FishAGuyManMachine",
        "FishAGuyNoKingsInOrbit",
        "FishAGuyNothingWrongWithMegacorpos",
        "FishAGuyTomfooleryRequired",
        "FishAGuyUndefinedTheory",
        "FishAGuyGetItDone",
        "FishAGuyLavaland",
        "FishAGuyLowfunk",
        "FishAGuyMotive",
        "FishAGuyRadify",
        "FishAGuySeclusion",
        "FishAGuyTheStation",
        "FishEnigma",
        "FishCodeSierra",
    ];

    [Test]
    [TestOf(typeof(JukeboxPrototype))]
    public async Task CatalogContainsLicensedTracks()
    {
        var protoMan = Server.ProtoMan;
        var resources = Server.ResolveDependency<IResourceManager>();
        var required = UpstreamTracks
            .Concat(ClassicSs13AndFishTracks)
            .Concat(CommunitySsSoundtrackTracks)
            .ToArray();

        await Server.WaitAssertion(() =>
        {
            var tracks = protoMan.EnumeratePrototypes<JukeboxPrototype>().ToDictionary(p => p.ID);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(tracks.Keys, Is.SupersetOf(required),
                    "Jukebox catalog is missing required licensed tracks.");

                foreach (var id in required)
                {
                    Assert.That(tracks[id].Name, Is.Not.Null.And.Not.Empty, $"{id} needs a display name.");
                    Assert.That(resources.ContentFileExists(tracks[id].Path.Path), Is.True,
                        $"Missing audio resource for {id}: {tracks[id].Path.Path}");
                }

                foreach (var track in tracks.Values)
                {
                    Assert.That(ReadVorbisChannelCount(resources, track.Path.Path), Is.EqualTo(1),
                        $"{track.ID} must be mono so positional jukebox audio can place it: {track.Path.Path}");
                }
            }
        });
    }

    [Test]
    [TestOf(typeof(JukeboxComponent))]
    public async Task JukeboxEntityUsesOriginalPlayerNotTapePlayer()
    {
        var jukebox = await Spawn("Jukebox");

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SEntMan.HasComponent<JukeboxComponent>(jukebox), Is.True,
                    "Jukebox entity must use JukeboxComponent.");
                Assert.That(SEntMan.HasComponent<TapePlayerComponent>(jukebox), Is.False,
                    "Station jukebox must not use Sunrise TapePlayer.");
            }
        });
    }

    [Test]
    [TestOf(typeof(JukeboxSystem))]
    public async Task SelectPlayAndStopTrack()
    {
        var jukebox = await Spawn("Jukebox");
        JukeboxSystem system = null!;
        var played = false;

        await Server.WaitPost(() =>
        {
            system = SEntMan.System<JukeboxSystem>();
            system.SetSelectedTrack(SEntity<JukeboxComponent>(jukebox).AsNullable(), "FlipFlap");
        });

        await Server.WaitAssertion(() =>
        {
            var ent = SEntity<JukeboxComponent>(jukebox);
            Assert.That(ent.Comp.SelectedSongId, Is.EqualTo(new ProtoId<JukeboxPrototype>("FlipFlap")));
        });

        await Server.WaitPost(() =>
        {
            played = system.TryPlay(SEntity<JukeboxComponent>(jukebox).AsNullable());
        });

        await Server.WaitAssertion(() =>
        {
            var ent = SEntity<JukeboxComponent>(jukebox);
            Assert.That(played, Is.True);
            Assert.That(system.IsPlaying(ent.AsNullable()), Is.True);
            Assert.That(ent.Comp.AudioStream, Is.Not.Null);
        });

        await Server.WaitPost(() =>
        {
            system.SetSelectedTrack(SEntity<JukeboxComponent>(jukebox).AsNullable(), "Tintin");
        });

        await Server.WaitAssertion(() =>
        {
            var ent = SEntity<JukeboxComponent>(jukebox);
            Assert.That(ent.Comp.SelectedSongId, Is.EqualTo(new ProtoId<JukeboxPrototype>("Tintin")));
            Assert.That(system.IsPlaying(ent.AsNullable()), Is.False,
                "Selecting a track while playing must stop the current stream.");
        });

        await Server.WaitPost(() =>
        {
            played = system.TryPlay(SEntity<JukeboxComponent>(jukebox).AsNullable());
        });

        await Server.WaitAssertion(() =>
        {
            var ent = SEntity<JukeboxComponent>(jukebox);
            Assert.That(played, Is.True);
            Assert.That(system.IsPlaying(ent.AsNullable()), Is.True);
        });

        await Server.WaitPost(() =>
        {
            system.Stop(SEntity<JukeboxComponent>(jukebox).AsNullable());
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(system.IsPlaying(SEntity<JukeboxComponent>(jukebox).AsNullable()), Is.False);
        });
    }

    /// <summary>
    /// Vorbis identification header stores the channel count 11 bytes after the packet marker.
    /// Positional jukebox playback rejects stereo sources.
    /// </summary>
    private static int ReadVorbisChannelCount(IResourceManager resources, ResPath path)
    {
        using var stream = resources.ContentFileRead(path);
        var buffer = new byte[65536];
        var read = stream.Read(buffer, 0, buffer.Length);
        ReadOnlySpan<byte> marker = [0x01, (byte) 'v', (byte) 'o', (byte) 'r', (byte) 'b', (byte) 'i', (byte) 's'];

        for (var i = 0; i <= read - 12; i++)
        {
            if (!buffer.AsSpan(i, marker.Length).SequenceEqual(marker))
                continue;

            return buffer[i + 11];
        }

        return -1;
    }
}
