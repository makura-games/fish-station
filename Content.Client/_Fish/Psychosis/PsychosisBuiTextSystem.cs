using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared._Fish.Psychosis;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Psychosis;

public sealed partial class PsychosisBuiTextSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int DistortionThreshold = 66;
    private const int FullDistortionLevel = 99;

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

        if (_psychosisLevel < DistortionThreshold)
        {
            RestoreTexts();
            return;
        }

        var level = Math.Clamp((int) _psychosisLevel, DistortionThreshold, FullDistortionLevel);
        var progress = (level - DistortionThreshold) / (float) (FullDistortionLevel - DistortionThreshold);
        var strength = 0.02f + 0.98f * progress * progress;

        PsychosisBuiWindowExtensions.RemoveClosedControls();
        _controlsToVisit.Clear();
        _activeControls.Clear();
        // Fish-edit: только окна игровых сущностей; HUD, ахелп, админка и настройки не обходятся.
        foreach (var (control, bui) in PsychosisBuiWindowExtensions.TrackedControls)
        {
            if (!bui.IsOpened || !control.IsInsideTree ||
                _player.LocalSession?.AttachedEntity == null)
                continue;

            if (!_userInterface.TryGetOpenUi(bui.Owner, bui.UiKey, out var openBui) || openBui != bui)
                continue;

            _controlsToVisit.Add(control);
        }

        for (var i = 0; i < _controlsToVisit.Count; i++)
        {
            var control = _controlsToVisit[i];
            if (!_activeControls.Add(control))
                continue;

            DistortText(control, level, strength);
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
            RestoreText(control);

        _controlsToVisit.Clear();
        _activeControls.Clear();
        _staleControls.Clear();
    }

    private void DistortText(Control control, int level, float strength)
    {
        var text = control switch
        {
            Label label => label.TextMemory.ToString(),
            RichTextLabel richText => richText.Text,
            _ => null,
        };

        if (text == null)
            return;

        var seed = 0;
        var hasPrevious = _distortedTexts.TryGetValue(control, out var previous);
        if (hasPrevious && text == previous.Distorted)
        {
            if (previous.Level == level)
                return;

            text = previous.Original;
            seed = previous.Seed;
        }
        else
        {
            _distortedTexts.Remove(control);
            if (string.IsNullOrEmpty(text))
                return;

            seed = _random.Next();
        }

        // Fish-edit: один seed сохраняет уже искажённые буквы при росте уровня, без мерцания каждый кадр.
        var random = new Random(seed);
        var distorted = control is RichTextLabel
            ? DistortMarkup(text, random, strength)
            : DistortPlainText(text, random, strength);

        _distortedTexts[control] = new DistortedText(text, distorted, level, seed);
        SetText(control, distorted);
    }

    private static string DistortMarkup(string markup, Random random, float strength)
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
            builder.Append(FormattedMessage.EscapeText(DistortPlainText(text, random, strength)));
        }

        return builder.ToString();
    }

    private static string DistortPlainText(string text, Random random, float strength)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var original = chars[i];
            if (!char.IsLetterOrDigit(original))
                continue;

            var threshold = random.NextDouble();
            // Fish-edit: число вызовов генератора не зависит от силы искажения.
            var replacement = char.IsDigit(original)
                ? (char) ('0' + random.Next(10))
                : (char) ('а' + random.Next(32));
            if (replacement == original)
                replacement = char.IsDigit(original)
                    ? (char) ('0' + (replacement - '0' + 1) % 10)
                    : (char) ('а' + (replacement - 'а' + 1) % 32);

            if (threshold < strength)
                chars[i] = replacement;
        }

        return new string(chars);
    }

    private static void SetText(Control control, string text)
    {
        if (control is Label label)
            label.Text = text;
        else if (control is RichTextLabel richText)
            richText.Text = text;
    }

    private void RestoreText(Control control)
    {
        if (!_distortedTexts.Remove(control, out var previous))
            return;

        if (control is Label label && label.TextMemory.ToString() == previous.Distorted ||
            control is RichTextLabel richText && richText.Text == previous.Distorted)
            SetText(control, previous.Original);
    }

    private void RestoreTexts()
    {
        _staleControls.Clear();
        foreach (var control in _distortedTexts.Keys)
            _staleControls.Add(control);

        foreach (var control in _staleControls)
            RestoreText(control);

        _staleControls.Clear();
    }

    private readonly record struct DistortedText(string Original, string Distorted, int Level, int Seed);
}
