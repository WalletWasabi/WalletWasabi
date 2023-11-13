using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace WalletWasabi.Tests.UnitTests.Fluent;

/// <summary>
/// Set of extension methods to simplify usage of Avalonia.Headless platform.
/// </summary>
internal static class HeadlessWindowExtensions
{
    /// <summary>
    /// Simulates click on the headless window/toplevel.
    /// </summary>
    public static void Click(
        this TopLevel topLevel,
        Control relativeTo,
        MouseButton button = MouseButton.Left,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var point = new Point(relativeTo.Bounds.Width / 2, relativeTo.Bounds.Height / 2);
        var translatePoint = relativeTo.TranslatePoint(point, topLevel);
        if (translatePoint is not null)
        {
            topLevel.MouseDown(translatePoint.Value, button, modifiers);
            topLevel.MouseUp(translatePoint.Value, button, modifiers);
        }
    }

    /// <summary>
    /// Simulates mouse down on the headless window/toplevel.
    /// </summary>
    public static void MouseDown(
        this TopLevel topLevel,
        Control relativeTo,
        Point point,
        MouseButton button,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var translatePoint = relativeTo.TranslatePoint(point, topLevel);
        if (translatePoint is not null)
        {
            topLevel.MouseDown(translatePoint.Value, button, modifiers);
        }
    }

    /// <summary>
    /// Simulates mouse move on the headless window/toplevel.
    /// </summary>
    public static void MouseMove(
        this TopLevel topLevel,
        Control relativeTo,
        Point point,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var translatePoint = relativeTo.TranslatePoint(point, topLevel);
        if (translatePoint is not null)
        {
            topLevel.MouseMove(translatePoint.Value, modifiers);
        }
    }

    /// <summary>
    /// Simulates mouse up on the headless window/toplevel.
    /// </summary>
    public static void MouseUp(
        this TopLevel topLevel,
        Control relativeTo,
        Point point,
        MouseButton button,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var translatePoint = relativeTo.TranslatePoint(point, topLevel);
        if (translatePoint is not null)
        {
            topLevel.MouseUp(translatePoint.Value, button, modifiers);
        }
    }

    /// <summary>
    /// Simulates mouse wheel on the headless window/toplevel.
    /// </summary>
    public static void MouseWheel(
        this TopLevel topLevel,
        Control relativeTo,
        Point point,
        Vector delta,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var translatePoint = relativeTo.TranslatePoint(point, topLevel);
        if (translatePoint is not null)
        {
            topLevel.MouseWheel(translatePoint.Value, delta, modifiers);
        }
    }
}
