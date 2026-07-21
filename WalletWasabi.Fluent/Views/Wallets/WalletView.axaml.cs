using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets;

public class WalletView : UserControl
{
	public WalletView()
	{
		InitializeComponent();

		this.GetObservable(BoundsProperty)
			.Subscribe(bounds =>
			{
				bool isMobile = bounds.Width > 0 && bounds.Width <= 600;
				if (isMobile)
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
