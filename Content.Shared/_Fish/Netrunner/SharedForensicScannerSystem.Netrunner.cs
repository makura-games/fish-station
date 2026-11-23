using System.Text;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Forensics.Components;

namespace Content.Shared._Fish.Netrunner
{
    /// <summary>Collects successful netrunner intervention logs for the forensic scanner.</summary>
    [ByRefEvent]
    public readonly record struct NetrunnerForensicScanEvent(List<string> Anomalies);
}

#pragma warning disable IDE0130 // Fish-edit: Расширяем криминалистический сканер из папки Fish.
namespace Content.Shared.Forensics.Systems
{
    public sealed partial class SharedForensicScannerSystem
    {
        private void ScanNetrunnerAnomalies(Entity<ForensicScannerComponent> scanner, EntityUid target)
        {
            var anomalies = new List<string>();
            var ev = new NetrunnerForensicScanEvent(anomalies);
            RaiseLocalEvent(target, ref ev);
            scanner.Comp.NetrunnerAnomalies = anomalies;
            DirtyField(scanner.AsNullable(), nameof(ForensicScannerComponent.NetrunnerAnomalies));
        }

        private void AppendNetrunnerAnomalies(StringBuilder text, ForensicScannerComponent scanner)
        {
            text.AppendLine();
            text.AppendLine(Loc.GetString("fish-netrunner-forensic-anomalies"));
            foreach (var anomaly in scanner.NetrunnerAnomalies)
            {
                text.AppendLine(anomaly);
            }
        }

        private void ClearNetrunnerAnomalies(Entity<ForensicScannerComponent> scanner)
        {
            scanner.Comp.NetrunnerAnomalies = [];
            DirtyField(scanner.AsNullable(), nameof(ForensicScannerComponent.NetrunnerAnomalies));
        }
    }
}
