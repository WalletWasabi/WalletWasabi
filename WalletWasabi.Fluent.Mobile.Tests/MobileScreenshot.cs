using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

internal static class MobileScreenshot
{
	/// <summary>Captures real Skia pixels, not a reimplementation of the view.</summary>
	public static byte[] Capture(Window window, string name, string scenario, string contentKind = "bound-fixture")
	{
		Assert.Matches("^[a-z0-9-]+$", name);
		// Bindings, layout and compositor commits are drained before saving. Static
		// baselines compare settled states, never wall-clock-dependent transitions.
		for (var pass = 0; pass < 3; pass++)
		{
			Dispatcher.UIThread.RunJobs();
			MobileSnapshotState.Prepare(window);
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		}
		Dispatcher.UIThread.RunJobs();
		using var frame = window.CaptureRenderedFrame();
		Assert.NotNull(frame);
		Assert.True(frame.PixelSize.Width > 0 && frame.PixelSize.Height > 0);
		var directory = Environment.GetEnvironmentVariable("WASABI_MOBILE_TEST_ARTIFACTS")
			?? Path.Combine(AppContext.BaseDirectory, "mobile-previews");
		Directory.CreateDirectory(directory);
		var file = Path.Combine(directory, name + ".png");
		frame.Save(file);
		byte[] pixels;
		var colors = new HashSet<uint>();
		using (var buffer = frame.Lock())
		{
			Assert.Equal(32, buffer.Format.BitsPerPixel);
			var stride = checked(frame.PixelSize.Width * 4);
			Assert.True(buffer.RowBytes >= stride);
			pixels = new byte[checked(stride * frame.PixelSize.Height)];
			for (var row = 0; row < frame.PixelSize.Height; row++)
				Marshal.Copy(IntPtr.Add(buffer.Address, checked(row * buffer.RowBytes)), pixels, checked(row * stride), stride);
		}
		for (var index = 0; index < pixels.Length && colors.Count <= 256; index += 4)
			colors.Add(BinaryPrimitives.ReadUInt32LittleEndian(pixels.AsSpan(index, 4)));
		File.WriteAllText(Path.Combine(directory, name + ".frame.json"), JsonSerializer.Serialize(new
		{
			engine = "avalonia-headless-skia", contentKind, scenario, image = name + ".png",
			motion = "settled-transitions", caret = "hidden-for-static-snapshot",
			width = frame.PixelSize.Width, height = frame.PixelSize.Height,
			dipWidth = window.ClientSize.Width, dipHeight = window.ClientSize.Height,
			scale = window.RenderScaling, theme = window.ActualThemeVariant.ToString(),
			culture = string.IsNullOrEmpty(CultureInfo.CurrentCulture.Name) ? "invariant" : CultureInfo.CurrentCulture.Name, timezone = TimeZoneInfo.Local.Id,
			commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local",
			sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
			observedColors = colors.Count
		}, new JsonSerializerOptions { WriteIndented = true }));
		Assert.True(colors.Count >= 32, $"{name}: the captured frame is blank or lacks rendered UI content.");
		return pixels;
	}

	public static bool IsShown(Visual visual) => visual.IsVisible && visual.GetVisualAncestors().All(parent => parent.IsVisible);

	public static void AssertNoHorizontalOverflow(Control view)
	{
		foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
		{
			if (!IsShown(scroll) || scroll.Viewport.Width <= 0) continue;
			Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1,
				$"{scroll.Name ?? scroll.GetType().Name}: {scroll.Extent.Width:F1} DIP content in {scroll.Viewport.Width:F1} DIP viewport.");
		}
	}

	public sealed class BindingErrors : ILogSink, IDisposable
	{
		private readonly ILogSink? _previous = Logger.Sink;
		private readonly ConcurrentQueue<string> _errors = new();
		public BindingErrors() => Logger.Sink = this;
		public IEnumerable<string> Errors => _errors;
		public bool IsEnabled(LogEventLevel level, string area) =>
			(area == "Binding" && level >= LogEventLevel.Warning) || (_previous?.IsEnabled(level, area) ?? false);
		public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
			Log(level, area, source, messageTemplate, Array.Empty<object?>());
		public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
		{
			if (area == "Binding" && level >= LogEventLevel.Warning)
			{
				var error = $"{source?.GetType().Name}: {messageTemplate} [{string.Join(", ", propertyValues.Select(value => value?.ToString()))}]";
				_errors.Enqueue(error);
				// Collection assertions abbreviate strings; preserve the full diagnostic
				// in the TRX output. These tests contain only isolated fixture data.
				Console.Error.WriteLine("NATIVE BINDING ERROR: " + error);
			}
			if (_previous?.IsEnabled(level, area) == true) _previous.Log(level, area, source, messageTemplate, propertyValues);
		}
		public void Dispose() { if (ReferenceEquals(Logger.Sink, this)) Logger.Sink = _previous; }
	}
}
