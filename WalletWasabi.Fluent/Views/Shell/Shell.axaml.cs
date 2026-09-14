using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Mobile.Views;

namespace WalletWasabi.Fluent.Views.Shell;

public class Shell : UserControl
{
	public Shell()
	{
		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() ||
			Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime ||
			Environment.GetEnvironmentVariable("WASABI_MOBILE_REFERENCE") == "1")
		{
			// The root keeps Avalonia's automatic safe-area padding. Do not add
			// simulated status bars or fixed iOS/Android inset values here.
			Content = new MobileShell();
		}
		else
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
