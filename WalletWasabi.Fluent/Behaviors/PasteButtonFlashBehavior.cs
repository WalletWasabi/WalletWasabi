using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Behaviors
{
	public class PasteButtonFlashBehavior : AttachedToVisualTreeBehavior<AnimatedButton>
	{
		private CancellationTokenSource? _cts;

		public static readonly StyledProperty<string> FlashAnimationProperty =
			AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(FlashAnimation));

		public static readonly StyledProperty<bool> IsEnabledProperty =
			AvaloniaProperty.Register<PasteButtonFlashBehavior, bool>(nameof(IsEnabled));

		public string FlashAnimation
		{
			get => GetValue(FlashAnimationProperty);
			set => SetValue(FlashAnimationProperty, value);
		}

		public bool IsEnabled
		{
			get => GetValue(IsEnabledProperty);
			set => SetValue(IsEnabledProperty, value);
		}

		protected override void OnAttachedToVisualTree()
		{
			if (AssociatedObject is null)
			{
				return;
			}

			RxApp.MainThreadScheduler.Schedule(async () => await CheckClipboardForValidAddressAsync());

			var mainWindow = ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow;
			Observable
				.FromEventPattern(mainWindow, nameof(mainWindow.Activated))
				.Subscribe(async _ => await CheckClipboardForValidAddressAsync());

			AssociatedObject.WhenAnyValue(x => x.AnimateIcon)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject.Classes.Remove(FlashAnimation));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_cts?.Cancel();
			_cts?.Dispose();
		}

		private async Task CheckClipboardForValidAddressAsync()
		{
			if (!IsEnabled)
			{
				return;
			}

			var textToPaste = await Application.Current.Clipboard.GetTextAsync();

			if (AddressStringParser.TryParse(textToPaste, Services.WalletManager.Network, out _))
			{
				await ExecuteAnimationAsync();
			}
		}

		private async Task ExecuteAnimationAsync()
		{
			if (AssociatedObject is null)
			{
				return;
			}

			_cts?.Cancel();
			_cts?.Dispose();
			_cts = new CancellationTokenSource();

			try
			{
				AssociatedObject.Classes.Add(FlashAnimation);
				await Task.Delay(1850, _cts.Token);
			}
			catch (OperationCanceledException)
			{
				// ignored
			}
			finally
			{
				AssociatedObject.Classes.Remove(FlashAnimation);
			}
		}
	}
}
