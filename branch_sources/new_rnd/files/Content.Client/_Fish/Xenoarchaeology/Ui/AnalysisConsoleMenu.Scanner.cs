using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Xenoarchaeology.Ui;

public sealed partial class AnalysisConsoleMenu
{
    // Переносной сканер использует тот же вывод графа и сведений, но не получает действий консоли.
    private EntityUid? _scannerOwner;
    private EntityUid? _scannerArtifact;
    private TimeSpan _scannerNextUpdate;
    private RichTextLabel? _scannerState;
    private readonly HashSet<string> _scannerTriggeredNodes = new();

    /// <summary>Uses the native artifact view for a handheld scanner, without console actions.</summary>
    public void SetScannerOwner(EntityUid owner)
    {
        if (!_ent.HasComponent<NodeScannerComponent>(owner))
        {
            Close();
            return;
        }

        _scannerOwner = owner;
        Title = Loc.GetString("node-scan-display-title");
        SetSize = new Vector2(820, 500);
        ServerButton.Visible = false;
        ExtractButton.Visible = false;
        ServerButton.Disabled = true;
        ExtractButton.Disabled = true;
        _scannerState = new RichTextLabel
        {
            Name = "ScannerState",
            HorizontalExpand = true,
        };
        BackPanel.Parent!.AddChild(_scannerState);
        UpdateScanner();
    }

    /// <summary>Refreshes the linked graph and selected node using the console's visibility rules.</summary>
    public void UpdateScanner()
    {
        if (!_ent.TryGetComponent<NodeScannerComponent>(_scannerOwner, out var scanner))
        {
            Close();
            return;
        }

        _scannerNextUpdate = _timing.CurTime + scanner.DisplayDataUpdateInterval;
        TryGetDisplayedArtifact(out var artifact);
        if (_scannerArtifact != artifact?.Owner)
            _currentNode = null;

        _scannerArtifact = artifact?.Owner;
        GraphControl.SetArtifact(artifact);
        ArtifactView.SetEntity(artifact);
        NoArtiLabel.Visible = artifact == null || !_xenoArtifact.GetAllNodes(artifact.Value).Any();
        NoArtiLabel.Text = Loc.GetString(artifact == null
            ? "node-scanner-artifact-non-connected"
            : "node-scan-no-data");
        NoneSelectedLabel.Visible = artifact != null && !NoArtiLabel.Visible && _currentNode == null;

        if (_scannerState != null)
        {
            _scannerState.Text = GetScannerStateText(artifact);
        }

        SetSelectedNode(_currentNode);
        if (NoArtiLabel.Visible)
            NoneSelectedLabel.Visible = false;
    }

    private string GetScannerStateText(Entity<XenoArtifactComponent>? artifact)
    {
        _scannerTriggeredNodes.Clear();
        if (artifact == null)
            return string.Empty;

        if (!_ent.TryGetComponent<XenoArtifactUnlockingComponent>(artifact.Value, out var unlocking))
            return Loc.GetString(artifact.Value.Comp.NextUnlockTime > _timing.CurTime
                ? "node-scanner-artifact-state-cooldown"
                : "node-scanner-artifact-state-ready");

        // Узлы текущей попытки активации отличаются от постоянно активных узлов графа.
        foreach (var index in unlocking.TriggeredNodeIndexes)
        {
            if (_xenoArtifact.TryGetNode(artifact.Value.AsNullable(), index, out var node))
                _scannerTriggeredNodes.Add(_xenoArtifact.GetNodeId(node.Value));
        }

        var status = Loc.GetString("node-scanner-artifact-state-unlocking");
        return _scannerTriggeredNodes.Count == 0
            ? status
            : $"{status}\n{string.Join(", ", _scannerTriggeredNodes)}";
    }

    private void UpdateScannerFrame()
    {
        TryGetDisplayedArtifact(out var artifact);
        // Изменение связи очищает старый граф сразу, сведения об узле обновляются с частотой сканера.
        if (_scannerArtifact == artifact?.Owner && _timing.CurTime < _scannerNextUpdate)
            return;

        UpdateScanner();
    }

    private bool TryGetDisplayedArtifact([NotNullWhen(true)] out Entity<XenoArtifactComponent>? artifact)
    {
        if (_scannerOwner is not { } scanner)
            return _artifactAnalyzer.TryGetArtifactFromConsole(_owner, out artifact);

        artifact = null;
        if (!_ent.TryGetComponent<NodeScannerConnectedComponent>(scanner, out var connected) ||
            !_ent.TryGetComponent<XenoArtifactComponent>(connected.AttachedTo, out var component))
            return false;

        artifact = (connected.AttachedTo, component);
        return true;
    }

    private void ValidateScannerSelection(ref Entity<XenoArtifactNodeComponent>? node)
    {
        if (_scannerOwner == null)
            return;

        if (node != null &&
            (!TryGetDisplayedArtifact(out var artifact) ||
             !_xenoArtifact.TryGetIndex(artifact.Value.AsNullable(), node.Value, out _)))
            node = null;

        _currentNode = node;
    }
}
