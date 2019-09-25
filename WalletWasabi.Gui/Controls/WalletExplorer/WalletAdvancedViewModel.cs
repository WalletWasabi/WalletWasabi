using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;

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
#pragma warning disable IDE0053 // Use expression body for lambda expressions
			ExpandItCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });
#pragma warning restore IDE0053 // Use expression body for lambda expressions
		}

		public ObservableCollection<WalletActionViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}
	}
}
