using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls
{
	public class PreviewItem : ContentControl
	{
		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<PreviewItem, string>(nameof(Text));

		public static readonly StyledProperty<Geometry> IconProperty =
			AvaloniaProperty.Register<PreviewItem, Geometry>(nameof(Icon));

		public static readonly StyledProperty<double> IconSizeProperty =
			AvaloniaProperty.Register<PreviewItem, double>(nameof(IconSize), 24);

		public static readonly StyledProperty<object?> CopyParameterProperty =
			AvaloniaProperty.Register<PreviewItem, object?>(nameof(CopyParameter));

		public static readonly StyledProperty<ICommand> CopyCommandProperty =
			AvaloniaProperty.Register<PreviewItem, ICommand>(nameof(CopyCommand));

		public static readonly StyledProperty<bool> CopyButtonVisibilityProperty =
			AvaloniaProperty.Register<PreviewItem, bool>(nameof(CopyButtonVisibility));

		private Stopwatch? _copyButtonPressedStopwatch;

		public PreviewItem()
		{
			CopyCommand = ReactiveCommand.CreateFromTask<object>(async obj =>
			{
				if (obj.ToString() is { } text)
				{
					_copyButtonPressedStopwatch = Stopwatch.StartNew();
					await Application.Current.Clipboard.SetTextAsync(text);
				}
			});

			this.WhenAnyValue(x => x.CopyParameter, x => x.IsPointerOver, (copyParameter, isPointerOver) => !string.IsNullOrEmpty(copyParameter?.ToString()) && isPointerOver)
				.Subscribe(async value =>
				{
					if (_copyButtonPressedStopwatch is { } sw)
					{
						var elapsedMilliseconds = sw.ElapsedMilliseconds;

						var millisecondsToWait = 1050 - (int)elapsedMilliseconds;

						if (millisecondsToWait > 0)
						{
							await Task.Delay(millisecondsToWait);
						}

						_copyButtonPressedStopwatch = null;
					}

					CopyButtonVisibility = value;
				});
		}

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
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

		public object? CopyParameter
		{
			get => GetValue(CopyParameterProperty);
			set => SetValue(CopyParameterProperty, value);
		}

		public ICommand CopyCommand
		{
			get => GetValue(CopyCommandProperty);
			set => SetValue(CopyCommandProperty, value);
		}

		public bool CopyButtonVisibility
		{
			get => GetValue(CopyButtonVisibilityProperty);
			set => SetValue(CopyButtonVisibilityProperty, value);
		}
	}
}
