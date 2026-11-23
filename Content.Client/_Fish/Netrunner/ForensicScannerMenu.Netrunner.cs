using System.Text;
using Content.Shared.Forensics.Components;

#pragma warning disable IDE0130 // Fish-edit: Расширяем окно криминалистического сканера из папки Fish.
namespace Content.Client.Forensics;

public sealed partial class ForensicScannerMenu
{
    private static void AppendNetrunnerAnomalies(StringBuilder text, ForensicScannerComponent scanner)
    {
        text.AppendLine();
        text.AppendLine(Loc.GetString("fish-netrunner-forensic-anomalies"));
        foreach (var anomaly in scanner.NetrunnerAnomalies)
        {
            text.AppendLine(anomaly);
        }
    }
}
