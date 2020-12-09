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

		private void Button_OnClick(object? sender, RoutedEventArgs e)
		{
			FluentLogger.ShowAndLogError(new Exception("Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book."));
		}
	}
}
