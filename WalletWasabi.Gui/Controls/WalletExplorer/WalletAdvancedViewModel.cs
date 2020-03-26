using ReactiveUI;
using System.Collections.ObjectModel;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletAdvancedViewModel : ViewModelBase
	{
		private ObservableCollection<WasabiDocumentTabViewModel> _items;
		private bool _isExpanded;

		public WalletAdvancedViewModel()
		{
			Items = new ObservableCollection<WasabiDocumentTabViewModel>();
		}

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public ObservableCollection<WasabiDocumentTabViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}
	}
}
