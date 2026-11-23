using Robust.Shared.Timing;

namespace Content.Client.Atmos.Overlays;

public sealed partial class GasTileFireOverlay
{
    private const int PhaseCount = 4;
    private readonly float[] _timer = new float[FireStates * PhaseCount];
    private readonly int[] _frameCounter = new int[FireStates * PhaseCount];

    private void InitializeFirePhases(int state)
    {
        for (var phase = 0; phase < PhaseCount; phase++)
            _frameCounter[state * PhaseCount + phase] = _frames[state].Length * phase / PhaseCount;
    }

    private static int FirePhaseSlot(int state, Vector2i tile)
        => state * PhaseCount + ((tile.X + 3 * tile.Y) & (PhaseCount - 1));

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var slot = 0; slot < _frameCounter.Length; slot++)
        {
            var delays = _frameDelays[slot / PhaseCount];
            if (delays.Length == 0)
                continue;

            _timer[slot] += args.DeltaSeconds;
            while (_timer[slot] >= delays[_frameCounter[slot]])
            {
                _timer[slot] -= delays[_frameCounter[slot]];
                _frameCounter[slot] = (_frameCounter[slot] + 1) % delays.Length;
            }
        }
    }
}
