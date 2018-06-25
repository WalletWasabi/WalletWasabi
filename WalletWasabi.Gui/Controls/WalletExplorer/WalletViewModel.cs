using System.Collections.ObjectModel;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : DocumentTabViewModel
	{
		public WalletViewModel(string name)
			: base(name)
		{
			_actions = new ObservableCollection<DocumentTabViewModel>
			{
				new SendActionViewModel(this),
				new ReceiveActionViewModel(this),
				new CoinJoinActionViewModel(this),
				new HistoryActionViewModel(this)
			};
		}

		public string Name { get; }

		private ObservableCollection<DocumentTabViewModel> _actions;

		public ObservableCollection<DocumentTabViewModel> Actions
		{
			get { return _actions; }
			set { this.RaiseAndSetIfChanged(ref _actions, value); }
		}
	}
}
