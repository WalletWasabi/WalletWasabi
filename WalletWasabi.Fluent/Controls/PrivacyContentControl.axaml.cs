using Avalonia;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls
{
	public enum ReplacementMode
	{
		Text,
		Icon
	}

	public class PrivacyContentControl : ContentControl
	{
		private const char PrivacyChar = '#';

		private CompositeDisposable? _disposable;

		public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
			AvaloniaProperty.Register<PrivacyContentControl, bool>(nameof(PrivacyModeEnabled));

		public static readonly StyledProperty<uint> NumberOfPrivacyCharsProperty =
			AvaloniaProperty.Register<PrivacyContentControl, uint>(nameof(NumberOfPrivacyChars), 5);

		public static readonly StyledProperty<string> PrivacyTextProperty =
			AvaloniaProperty.Register<PrivacyContentControl, string>(nameof(PrivacyText));

		public static readonly StyledProperty<ReplacementMode> PrivacyReplacementModeProperty =
			AvaloniaProperty.Register<PrivacyContentControl, ReplacementMode>(nameof(PrivacyReplacementMode));

		private bool PrivacyModeEnabled
		{
			get => GetValue(PrivacyModeEnabledProperty);
			set => SetValue(PrivacyModeEnabledProperty, value);
		}

		public uint NumberOfPrivacyChars
		{
			get => GetValue(NumberOfPrivacyCharsProperty);
			set => SetValue(NumberOfPrivacyCharsProperty, value);
		}

		private string PrivacyText
		{
			get => GetValue(PrivacyTextProperty);
			set => SetValue(PrivacyTextProperty, value);
		}

		public ReplacementMode PrivacyReplacementMode
		{
			get => GetValue(PrivacyReplacementModeProperty);
			set => SetValue(PrivacyReplacementModeProperty, value);
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			_disposable = new CompositeDisposable();

			Services.UiConfig
				.WhenAnyValue(x => x.PrivacyMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(value => PrivacyModeEnabled = value)
				.DisposeWith(_disposable);

			PrivacyText = new string(Enumerable.Repeat(PrivacyChar, (int) NumberOfPrivacyChars).ToArray());
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			_disposable?.Dispose();
			_disposable = null;
		}
	}
}
