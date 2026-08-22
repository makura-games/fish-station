using Content.Shared.Atmos;

namespace Content.Server.Atmos.Piping.Trinary.Components;

[RegisterComponent]
public sealed partial class GasRangeTemperatureFilterComponent : Component
{
    [DataField] public bool Enabled = true;

    [DataField("inlet")] public string InletName = "inlet";
    [DataField("bypass")] public string BypassName = "bypass";
    [DataField("filtered")] public string FilteredName = "filtered";

    [DataField] public float TransferRate = Atmospherics.MaxTransferRate;
    [DataField] public float MaxTransferRate = Atmospherics.MaxTransferRate;

    [DataField] public float LowTemperature = Atmospherics.TCMB;    // по умолчанию – минимальная
    [DataField] public float HighTemperature = 1000f;               // например, 1000 K
}