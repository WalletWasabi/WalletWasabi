using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

namespace WalletWasabi.Fluent.Behaviors
{
	public class MenuFlyoutSelectedItemBehavior : DisposingBehavior<MenuFlyout>
	{
		public static readonly StyledProperty<TimePeriodOption> CurrentTimePeriodOptionProperty =
			AvaloniaProperty.Register<MenuFlyoutSelectedItemBehavior, TimePeriodOption>(nameof(CurrentTimePeriodOption));

		public TimePeriodOption CurrentTimePeriodOption
		{
			get => GetValue(CurrentTimePeriodOptionProperty);
			set => SetValue(CurrentTimePeriodOptionProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.Opened))
				.Subscribe(_ =>
				{
					foreach (var item in AssociatedObject.Items)
					{
						if (item is MenuItem mi && (string) mi.Header == CurrentTimePeriodOption.FriendlyName())
						{
							mi.IsSelected = true;
						}
					}
				})
				.DisposeWith(disposables);
		}
	}
}
