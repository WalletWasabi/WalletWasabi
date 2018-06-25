using System;
using System.Collections.ObjectModel;
using System.Composition;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<WalletActionViewModel> _actions;

		public WalletViewModel(string name)
			: base(name)
		{
			_actions = new ObservableCollection<WalletActionViewModel>
			{
				new SendTabViewModel(this),
				new ReceiveTabViewModel(this),
				new CoinJoinTabViewModel(this),
				new HistoryTabViewModel(this)
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
}
