using Avalonia;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls
{
	public class PrivacyTextControl : TemplatedControl
	{
		private const char PrivacyChar = '#';

		private CompositeDisposable? _disposable;

		public static readonly StyledProperty<bool> PrivacyModeEnabledProperty =
			AvaloniaProperty.Register<PrivacyTextControl, bool>(nameof(PrivacyModeEnabled));

		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<PrivacyTextControl, string>(nameof(Text));

		public static readonly StyledProperty<string> TextBlockClassesProperty =
			AvaloniaProperty.Register<PrivacyContentControl, string>(nameof(TextBlockClasses));

		public static readonly StyledProperty<uint> NumberOfPrivacyCharsProperty =
			AvaloniaProperty.Register<PrivacyContentControl, uint>(nameof(NumberOfPrivacyChars), 5);

		public static readonly StyledProperty<string> PrivacyTextProperty =
			AvaloniaProperty.Register<PrivacyContentControl, string>(nameof(PrivacyText));

		private bool PrivacyModeEnabled
		{
			get => GetValue(PrivacyModeEnabledProperty);
			set => SetValue(PrivacyModeEnabledProperty, value);
		}

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public string TextBlockClasses
		{
			get => GetValue(TextBlockClassesProperty);
			set => SetValue(TextBlockClassesProperty, value);
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

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			if (TextBlockClasses is { } classes)
			{
				var textBlock = e.NameScope.Find<TextBlock>("PART_Text");
				textBlock.Classes.Add(classes);

				var privacyTextBlock = e.NameScope.Find<TextBlock>("PART_PrivacyText");
				privacyTextBlock.Classes.Add(classes);
			}
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

			PrivacyText =  new string(Enumerable.Repeat(PrivacyChar, (int) NumberOfPrivacyChars).ToArray());
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			_disposable?.Dispose();
			_disposable = null;
		}
	}
}
