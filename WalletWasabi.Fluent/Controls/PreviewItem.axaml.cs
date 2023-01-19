using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Controls;

public class PreviewItem : ContentControl
{
	public static readonly StyledProperty<string> LabelProperty =
		AvaloniaProperty.Register<PreviewItem, string>(nameof(Label));

	public static readonly StyledProperty<Geometry> IconProperty =
		AvaloniaProperty.Register<PreviewItem, Geometry>(nameof(Icon));

	public static readonly StyledProperty<double> IconSizeProperty =
		AvaloniaProperty.Register<PreviewItem, double>(nameof(IconSize), 24);

	public static readonly StyledProperty<bool> IsIconVisibleProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(IsIconVisible));

	public static readonly StyledProperty<string> TextValueProperty =
		AvaloniaProperty.Register<PreviewItem, string>(nameof(TextValue));

	public static readonly StyledProperty<ICommand> CopyCommandProperty =
		AvaloniaProperty.Register<PreviewItem, ICommand>(nameof(CopyCommand));

	public static readonly StyledProperty<bool> IsCopyButtonVisibleProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(IsCopyButtonVisible));

	public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
		AvaloniaProperty.Register<PreviewItem, bool>(nameof(PrivacyModeEnabled));

	private Stopwatch? _copyButtonPressedStopwatch;

	public PreviewItem()
	{
		CopyCommand = new AsyncRelayCommand(async () =>
		{
			if (Application.Current is { Clipboard: { } clipboard } && TextValue is { } text)
			{
				_copyButtonPressedStopwatch = Stopwatch.StartNew();
				await clipboard.SetTextAsync(text);
			}
		});

		this.WhenAnyValue(
				x => x.TextValue,
				x => x.IsPointerOver,
				x => x.PrivacyModeEnabled,
				(copyParameter, isPointerOver, privacyModeEnabled) => !string.IsNullOrEmpty(copyParameter?.ToString()) && isPointerOver && !privacyModeEnabled)
			.SubscribeAsync(async value =>
			{
				if (_copyButtonPressedStopwatch is { } sw)
				{
					var millisecondsToWait = Math.Max(1050 - (int)sw.ElapsedMilliseconds, 0);
					await Task.Delay(millisecondsToWait);
					_copyButtonPressedStopwatch = null;
				}

				IsCopyButtonVisible = value;
			});
	}

	public string Label
	{
		get => GetValue(LabelProperty);
		set => SetValue(LabelProperty, value);
	}

	public Geometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public double IconSize
	{
		get => GetValue(IconSizeProperty);
		set => SetValue(IconSizeProperty, value);
	}

	public bool IsIconVisible
	{
		get => GetValue(IsIconVisibleProperty);
		set => SetValue(IsIconVisibleProperty, value);
	}

	public string TextValue
	{
		get => GetValue(TextValueProperty);
		set => SetValue(TextValueProperty, value);
	}

	public ICommand CopyCommand
	{
		get => GetValue(CopyCommandProperty);
		set => SetValue(CopyCommandProperty, value);
	}

	public bool IsCopyButtonVisible
	{
		get => GetValue(IsCopyButtonVisibleProperty);
		set => SetValue(IsCopyButtonVisibleProperty, value);
	}

	public bool PrivacyModeEnabled
	{
		get => GetValue(PrivacyModeEnabledProperty);
		set => SetValue(PrivacyModeEnabledProperty, value);
	}
}
