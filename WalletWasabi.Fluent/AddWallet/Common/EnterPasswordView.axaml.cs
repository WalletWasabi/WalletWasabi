using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace WalletWasabi.Fluent.AddWallet.Common
{
	public class EnterPasswordView : ReactiveUserControl<EnterPasswordViewModel>
	{
		public EnterPasswordView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}