using System;
using System.Collections.ObjectModel;
using System.Composition;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : DocumentTabViewModel
	{
		private ObservableCollection<WalletActionViewModel> _actions;

		public WalletViewModel(string name)
			: base(name)
		{
			_actions = new ObservableCollection<WalletActionViewModel>
			{
				new SendActionViewModel(this),
				new ReceiveActionViewModel(this),
				new CoinJoinActionViewModel(this),
				new HistoryActionViewModel(this)
			};

			foreach (var vm in _actions)
			{
				vm.DisplayActionTab();
			}
		}

		public string Name { get; }

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get { return _actions; }
			set { this.RaiseAndSetIfChanged(ref _actions, value); }
		}
	}

	public class WalletActionViewModel : DocumentTabViewModel
	{
		public WalletViewModel Wallet { get; }

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Wallet = walletViewModel;
			DoItCommand = ReactiveCommand.Create(DisplayActionTab);
		}

		public ReactiveCommand DoItCommand { get; }

		public void DisplayActionTab()
		{
			IoC.Get<IShell>().AddOrSelectDocument(this);
		}
	}

	public class SendActionViewModel : WalletActionViewModel
	{
		public SendActionViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel) { }
	}

	public class ReceiveActionViewModel : WalletActionViewModel
	{
		public ReceiveActionViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel) { }
	}

	public class HistoryActionViewModel : WalletActionViewModel
	{
		public HistoryActionViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel) { }
	}

	public class CoinJoinActionViewModel : WalletActionViewModel
	{
		public CoinJoinActionViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel) { }
	}
}
