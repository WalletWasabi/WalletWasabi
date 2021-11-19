using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Behaviors
{
	public class PasteButtonFlashBehavior : DisposingBehavior<AnimatedButton>
	{
		public static readonly StyledProperty<string> FlashAnimationProperty =
			AvaloniaProperty.Register<PasteButtonFlashBehavior, string>(nameof(FlashAnimation));

		public string FlashAnimation
		{
			get => GetValue(FlashAnimationProperty);
			set => SetValue(FlashAnimationProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			RxApp.MainThreadScheduler.Schedule(async () => await CheckClipboardForValidAddressAsync());

			var mainWindow = ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow;
			Observable
				.FromEventPattern(mainWindow, nameof(mainWindow.Activated))
				.Subscribe(async _ => await CheckClipboardForValidAddressAsync())
				.DisposeWith(disposables);

			AssociatedObject.WhenAnyValue(x => x.AnimateIcon)
				.Where(x => x)
				.Subscribe(_ => CancelAnimation())
				.DisposeWith(disposables);
		}

		private async Task CheckClipboardForValidAddressAsync()
		{
			if (Services.UiConfig.AutoPaste)
			{
				return;
			}

			var textToPaste = await Application.Current.Clipboard.GetTextAsync();

			if (AddressStringParser.TryParse(textToPaste, Services.WalletManager.Network, out _))
			{
				ExecuteAnimation();
			}
		}

		private void CancelAnimation()
		{
			if (AssociatedObject is null)
			{
				return;
			}

			if (AssociatedObject.Classes.Contains(FlashAnimation))
			{
				AssociatedObject.Classes.Remove(FlashAnimation);
			}
		}

		private void ExecuteAnimation()
		{
			if (AssociatedObject is null)
			{
				return;
			}

			CancelAnimation();

			AssociatedObject.Classes.Add(FlashAnimation);
		}
	}
}
