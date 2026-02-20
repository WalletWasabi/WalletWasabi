using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Views.Wallets.Send;

public class SendView : UserControl
{
	public SendView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private async void AmountTb_GotFocusAsync(object? sender, GotFocusEventArgs e)
	{		
		if (DataContext is SendViewModel vm)
		{
			var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

			if (focused is CurrencyEntryBox currencyEntryBox)
			{
				string content = await ApplicationHelper.GetTextAsync();

				if (currencyEntryBox.IsFiat)
				{
					var usd = ClipboardObserver.ParseToUsd(content);
					if (usd is not null)
					{
						vm.UsdContent = usd.Value.ToString("0.00");
					}
				}
				else
				{
					var latestBalance = vm.BalanceLatest;
					if (latestBalance is not null)
					{
						var btc = ClipboardObserver.ParseToMoney(content, latestBalance.Btc);
						if (btc is not null)
						{
							vm.BitcoinContent = btc;
						}
					}
				}
			}
		}
	}
}
