using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace WalletWasabi.Fluent.Views.Wallets.Send;

public class TransactionSummary : UserControl
{
	public TransactionSummary()
	{
		InitializeComponent();
		this.GetObservable(BoundsProperty).Subscribe(bounds =>
		{
			if (bounds.Width > 0 && bounds.Width <= 600)
			{
				if (!Classes.Contains("mobile"))
				{
					Classes.Add("mobile");
				}
			}
			else
			{
				Classes.Remove("mobile");
			}
		});
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
