using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Views.Wallets
{
	public class LoginView : UserControl
	{
		public LoginView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
