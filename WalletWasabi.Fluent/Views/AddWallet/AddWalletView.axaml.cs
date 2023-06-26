using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.AddWallet;

public class AddWalletView : UserControl
{
	public AddWalletView()
	{
		InitializeComponent();
	}

	public static readonly StyledProperty<ICommand> ImportWalletProperty = AvaloniaProperty.Register<AddWalletView, ICommand>("ImportWallet");

	public ICommand ImportWallet
	{
		get => GetValue(ImportWalletProperty);
		set => SetValue(ImportWalletProperty, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
