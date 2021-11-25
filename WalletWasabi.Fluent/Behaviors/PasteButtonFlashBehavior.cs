using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Behaviors
{
	public class PasteButtonFlashBehavior : AttachedToVisualTreeBehavior<AnimatedButton>
	{
		private string? _lastFlashedOn;

		public static readonly StyledProperty<string> FlashAnimationProperty =
			AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(FlashAnimation));

		public static readonly StyledProperty<string> CurrentAddressProperty =
			AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(CurrentAddress));

		public string FlashAnimation
		{
			get => GetValue(FlashAnimationProperty);
			set => SetValue(FlashAnimationProperty, value);
		}

		public string CurrentAddress
		{
			get => GetValue(CurrentAddressProperty);
			set => SetValue(CurrentAddressProperty, value);
		}

		protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
		{
			Dispatcher.UIThread.Post(async () => await CheckClipboardForValidAddressAsync(), DispatcherPriority.Loaded);

			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
			{
				var mainWindow  = lifetime.MainWindow;

				Observable
					.FromEventPattern(mainWindow, nameof(mainWindow.Activated)).Select(_ => Unit.Default)
					.Merge(this.WhenAnyValue(x => x.CurrentAddress).Select(_ => Unit.Default))
					.Throttle(TimeSpan.FromMilliseconds(100))
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(async _ => await CheckClipboardForValidAddressAsync(false))
					.DisposeWith(disposables);
			}

			AssociatedObject?.WhenAnyValue(x => x.AnimateIcon)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject.Classes.Remove(FlashAnimation))
				.DisposeWith(disposables);
		}

		private async Task CheckClipboardForValidAddressAsync(bool schedule = true)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			var textToPaste = await Application.Current.Clipboard.GetTextAsync();

			if (_lastFlashedOn != textToPaste)
			{
				AssociatedObject?.Classes.Remove(FlashAnimation);

				if (textToPaste != CurrentAddress &&
				    AddressStringParser.TryParse(textToPaste, Services.WalletManager.Network, out _))
				{
					AssociatedObject?.Classes.Add(FlashAnimation);

					ToolTip.SetTip(AssociatedObject!, $"Paste BTC Address:\r\n{textToPaste}");
				}
				else
				{
					ToolTip.SetTip(AssociatedObject!, "Paste");
				}

				_lastFlashedOn = textToPaste;
			}

			if (schedule)
			{
				await Task.Delay(500);

				RxApp.MainThreadScheduler.Schedule(async ()=> await CheckClipboardForValidAddressAsync());
			}
		}
	}
}
