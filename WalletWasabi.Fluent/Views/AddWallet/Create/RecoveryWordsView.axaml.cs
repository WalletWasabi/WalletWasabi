using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.AddWallet.Create;

public class RecoveryWordsView : UserControl
{
	public RecoveryWordsView()
	{
		InitializeComponent();

		this.GetObservable(BoundsProperty)
			.Subscribe(bounds =>
			{
				bool isMobile = bounds.Width > 0 && bounds.Width <= 600;
				WalletWasabi.Logging.Logger.LogInfo($"[RecoveryWordsView] Bounds changed: {bounds.Width}x{bounds.Height}, isMobile: {isMobile}");
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
