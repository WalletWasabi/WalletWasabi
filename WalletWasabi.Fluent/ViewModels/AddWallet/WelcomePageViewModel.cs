using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Welcome")]
	public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
	{
		private const int NumberOfPages = 5;

		[AutoNotify] private int _selectedIndex;

		public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
		{
			SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
			EnableBack = false;

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

			SelectedIndex = 0;

			NextCommand = ReactiveCommand.Create(() => SelectedIndex++, this.WhenAnyValue(x => x.SelectedIndex).Select(c => c < NumberOfPages - 1));
			PrevCommand = ReactiveCommand.Create(() => SelectedIndex--, this.WhenAnyValue(x => x.SelectedIndex).Select(c => c > 0));
		}

		public ICommand GetStartedCommand { get; }

		public ICommand PrevCommand { get; }
	}
}
