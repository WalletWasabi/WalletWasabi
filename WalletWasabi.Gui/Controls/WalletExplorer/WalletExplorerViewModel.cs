using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Gui.Extensions;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[Export]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IActivatableExtension
	{
		private ViewModelBase _selectedItem;
		private ObservableCollection<WalletViewModelBase> _wallets;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";

			_wallets = new ObservableCollection<WalletViewModelBase>();

			CollapseAllCommand = ReactiveCommand.Create(() =>
			{
				foreach (var wallet in Wallets)
				{
					wallet.IsExpanded = false;
				}
			});

			LurkingWifeModeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var global = Locator.Current.GetService<Global>();

				global.UiConfig.LurkingWifeMode = !global.UiConfig.LurkingWifeMode;
				await global.UiConfig.ToFileAsync();
			});

			LurkingWifeModeCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public override Location DefaultLocation => Location.Right;

		public ObservableCollection<WalletViewModelBase> Wallets
		{
			get => _wallets;
			set => this.RaiseAndSetIfChanged(ref _wallets, value);
		}

		public ViewModelBase SelectedItem
		{
			get => _selectedItem;
			set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
		}

		public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

		public ReactiveCommand<Unit, Unit> LurkingWifeModeCommand { get; }

		internal void RemoveWallet (WalletViewModelBase wallet)
		{
			Wallets.Remove(wallet);
		}

		internal void OpenWallet(Wallet wallet, bool receiveDominant)
		{
			WalletViewModel walletViewModel = new WalletViewModel(wallet, receiveDominant);
			Wallets.InsertSorted(walletViewModel);
			walletViewModel.OnWalletOpened();

			// TODO if we ever implement closing a wallet OnWalletClosed needs to be called
			// to prevent memory leaks.
		}

		private void LoadWallets()
		{
			Wallets.Clear();

			var global = Locator.Current.GetService<Global>();

			var directoryInfo = new DirectoryInfo(global.WalletsDir);
			var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
			foreach (var file in walletFiles)
			{
				var wallet = new ClosedWalletViewModel(file.FullName);

				Wallets.InsertSorted(wallet);
			}
		}

		public void BeforeActivation()
		{
		}

		public void Activation()
		{
			IoC.Get<IShell>().MainPerspective.AddOrSelectTool(this);

			LoadWallets();
		}
	}
}
