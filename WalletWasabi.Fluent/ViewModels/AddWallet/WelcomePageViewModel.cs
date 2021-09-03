using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Welcome")]
	public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
	{
		public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
		{
			GetStartedCommand = ReactiveCommand.Create(() =>
			{
				if (!Services.WalletManager.HasWallet())
				{
					Navigate().To(addWalletPage);
				}
				else
				{
					Close();
				}
			});
		}

		public ICommand GetStartedCommand { get; }
	}
}