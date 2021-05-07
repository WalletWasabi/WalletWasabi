using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Payment successful")]
	public partial class SendSuccessViewModel : RoutableViewModel
	{
		private readonly WalletViewModel _owner;
		private readonly SmartTransaction _finalTransaction;

		public SendSuccessViewModel(WalletViewModel owner, SmartTransaction finalTransaction)
		{
			_owner = owner;
			_finalTransaction = finalTransaction;

			NextCommand = ReactiveCommand.Create(OnNext);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		}

		private void OnNext()
		{
			Navigate().Clear();

			_owner.History.SelectTransaction(_finalTransaction.GetHash());
		}
	}
}
