using System.Collections.ObjectModel;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : DocumentTabViewModel
	{
		public WalletViewModel (string name)
			: base(name)
		{
			_actions = new ObservableCollection<DocumentTabViewModel>();
			_actions.Add(new SendActionViewModel(this));
			_actions.Add(new ReceiveActionViewModel(this));
			_actions.Add(new CoinJoinActionViewModel(this));
			_actions.Add(new HistoryActionViewModel(this));
		}

		public string Name { get; }

		private ObservableCollection<DocumentTabViewModel> _actions;
		public ObservableCollection<DocumentTabViewModel> Actions
		{
			get { return _actions; }
			set { this.RaiseAndSetIfChanged(ref _actions, value); }
		}
	}

	public class WalletActionViewModel : DocumentTabViewModel
	{
		public  WalletViewModel Wallet { get; }

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Wallet = walletViewModel;
			DoItCommand = ReactiveCommand.Create(DisplayActionTab);
		}

		public ReactiveCommand DoItCommand { get; }

		public void DisplayActionTab()
		{
			IoC.Get<IShell>().AddDocument(this);
		}
	}

	public class SendActionViewModel : WalletActionViewModel 
	{
		public SendActionViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel){}
	}

	public class ReceiveActionViewModel : WalletActionViewModel 
	{
		public ReceiveActionViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel){}
	}

	public class HistoryActionViewModel : WalletActionViewModel 
	{
		public HistoryActionViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel){}
	}

	public class CoinJoinActionViewModel : WalletActionViewModel 
	{
		public CoinJoinActionViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel){}
	}
}