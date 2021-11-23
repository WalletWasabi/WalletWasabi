using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Behaviors
{
	public class PasteButtonFlashBehavior : DisposingBehavior<AnimatedButton>
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

		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			Dispatcher.UIThread.Post(async () => await CheckClipboardForValidAddressAsync(), DispatcherPriority.Loaded);

			var mainWindow = ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow;
			Observable
				.FromEventPattern(mainWindow, nameof(mainWindow.Activated))
				.Subscribe(async _ => await CheckClipboardForValidAddressAsync())
				.DisposeWith(disposables);

			AssociatedObject.WhenAnyValue(x => x.AnimateIcon)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject.Classes.Remove(FlashAnimation))
				.DisposeWith(disposables);

			Disposable.Create(() =>
				{
					_cts?.Cancel();
					_cts?.Dispose();
				})
				.DisposeWith(disposables);
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
