using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Logging;
using System;
using System.IO;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletAdvancedViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<WasabiDocumentTabViewModel> _items;

		private bool _isExpanded;

		public ReactiveCommand<Unit, bool> ExpandItCommand { get; }

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public WalletAdvancedViewModel(Wallet wallet) : base(wallet.WalletName)
		{
			Items = new ObservableCollection<WasabiDocumentTabViewModel>();

			ExpandItCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);

			ExpandItCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ObservableCollection<WasabiDocumentTabViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}
	}
}
