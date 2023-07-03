using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System.Globalization;

namespace WalletWasabi.Fluent.Views.AddWallet.Create;

public class ConfirmRecoveryWordsView : UserControl
{
	public ConfirmRecoveryWordsView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
