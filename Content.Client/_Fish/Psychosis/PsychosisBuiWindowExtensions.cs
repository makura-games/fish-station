using System;
using System.Collections.Generic;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

// Fish-edit: пространство имён родителя позволяет перехватить создание BUI только в Content.Client.
namespace Content.Client;

/// <summary>
/// Сохраняет привязку игровых окон к BUI, не изменяя код движка.
/// </summary>
public static class PsychosisBuiWindowExtensions
{
    private static readonly Dictionary<Control, BoundUserInterface> Controls = new();

    public static IEnumerable<KeyValuePair<Control, BoundUserInterface>> TrackedControls => Controls;

    public static void RemoveClosedControls()
    {
        var stale = new List<Control>();
        foreach (var (control, bui) in Controls)
        {
            if (!bui.IsOpened || !control.IsInsideTree)
                stale.Add(control);
        }

        foreach (var control in stale)
            Controls.Remove(control);
    }

    public static T CreateWindow<T>(this BoundUserInterface bui) where T : BaseWindow, new()
    {
        var window = BoundUserInterfaceExt.CreateWindow<T>(bui);
        Controls[window] = bui;
        return window;
    }

    public static T CreateWindowCenteredLeft<T>(this BoundUserInterface bui) where T : BaseWindow, new()
    {
        var window = BoundUserInterfaceExt.CreateWindowCenteredLeft<T>(bui);
        Controls[window] = bui;
        return window;
    }

    public static T CreateWindowCenteredRight<T>(this BoundUserInterface bui) where T : BaseWindow, new()
    {
        var window = BoundUserInterfaceExt.CreateWindowCenteredRight<T>(bui);
        Controls[window] = bui;
        return window;
    }

    public static T CreateDisposableControl<T>(this BoundUserInterface bui) where T : Control, IDisposable, new()
    {
        var control = BoundUserInterfaceExt.CreateDisposableControl<T>(bui);
        Controls[control] = bui;
        return control;
    }
}
