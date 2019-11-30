using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletAdvancedViewModel : WalletActionViewModel
	{
		private ObservableCollection<WalletActionViewModel> _items;

		private bool _isExpanded;

		public ReactiveCommand<Unit, Unit> ExpandItCommand { get; }

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public WalletAdvancedViewModel(WalletViewModel walletViewModel) : base(walletViewModel.Name, walletViewModel)
		{
			Items = new ObservableCollection<WalletActionViewModel>();

			ExpandItCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });

			ExpandItCommand.ThrownExceptions.Subscribe(ex => Logger.LogError(ex));
		}

		public ObservableCollection<WalletActionViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}
	}
}
