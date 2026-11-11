using System.Collections.Generic;
using System.Text;
using Content.Shared._Fish.Psychosis;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Psychosis;

public sealed partial class PsychosisBuiTextSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly Dictionary<Control, DistortedText> _distortedTexts = new();
    private readonly List<Control> _controlsToVisit = new();
    private readonly HashSet<Control> _activeControls = new();
    private readonly List<Control> _staleControls = new();

    private float _psychosisLevel;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
    }

    public override void Shutdown()
    {
        RestoreTexts();
        base.Shutdown();
    }

    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        _psychosisLevel = ev.Level;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _controlsToVisit.Clear();
        _activeControls.Clear();
        foreach (var control in _userInterface.RegisteredControls)
            _controlsToVisit.Add(control);

        for (var i = 0; i < _controlsToVisit.Count; i++)
        {
            var control = _controlsToVisit[i];
            if (!_activeControls.Add(control))
                continue;

            if (_psychosisLevel >= 100f)
                DistortText(control);
            else
                RestoreText(control);

            foreach (var child in control.Children)
                _controlsToVisit.Add(child);
        }

        _staleControls.Clear();
        foreach (var control in _distortedTexts.Keys)
        {
            if (!_activeControls.Contains(control))
                _staleControls.Add(control);
        }

        foreach (var control in _staleControls)
            _distortedTexts.Remove(control);
    }

    private void DistortText(Control control)
    {
        if (control is Label label)
        {
            var original = label.TextMemory.ToString();
            if (_distortedTexts.TryGetValue(control, out var previous))
            {
                if (original == previous.Distorted)
                    return;

                _distortedTexts.Remove(control);
            }

            if (string.IsNullOrEmpty(original))
                return;

            var distorted = PsychosisTextDistortion.DistortToNonsense(original, _random);
            if (distorted == original)
                return;

            _distortedTexts[control] = new DistortedText(original, distorted);
            label.Text = distorted;
            return;
        }

        if (control is not RichTextLabel richText)
            return;

        var markup = richText.Text;
        if (string.IsNullOrEmpty(markup))
            return;

        if (_distortedTexts.TryGetValue(control, out var previousRichText))
        {
            if (markup == previousRichText.Distorted)
                return;

            _distortedTexts.Remove(control);
        }

        var distortedMarkup = DistortMarkup(markup);
        if (distortedMarkup == markup)
            return;

        _distortedTexts[control] = new DistortedText(markup, distortedMarkup);
        richText.Text = distortedMarkup;
    }

    private string DistortMarkup(string markup)
    {
        var message = FormattedMessage.FromMarkupPermissive(markup);
        var builder = new StringBuilder(markup.Length);
        foreach (var node in message)
        {
            if (!node.IsPlainText)
            {
                builder.Append(node);
                continue;
            }

            var text = node.Value.StringValue ?? string.Empty;
            builder.Append(FormattedMessage.EscapeText(PsychosisTextDistortion.DistortToNonsense(text, _random)));
        }

        return builder.ToString();
    }

    private void RestoreText(Control control)
    {
        if (!_distortedTexts.Remove(control, out var previous))
            return;

        if (control is Label label && label.TextMemory.ToString() == previous.Distorted)
            label.Text = previous.Original;
        else if (control is RichTextLabel richText && richText.Text == previous.Distorted)
            richText.Text = previous.Original;
    }

    private void RestoreTexts()
    {
        foreach (var control in _distortedTexts.Keys)
        {
            var previous = _distortedTexts[control];
            if (control is Label label && label.TextMemory.ToString() == previous.Distorted)
                label.Text = previous.Original;
            else if (control is RichTextLabel richText && richText.Text == previous.Distorted)
                richText.Text = previous.Original;
        }

        _distortedTexts.Clear();
    }

    private readonly record struct DistortedText(string Original, string Distorted);
}
