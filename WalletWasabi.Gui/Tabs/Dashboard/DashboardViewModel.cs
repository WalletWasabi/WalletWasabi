using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.DeveloperNews;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.Tabs.WalletManager.GenerateWallets;
using WalletWasabi.Gui.Tabs.WalletManager.LoadWallets;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.Dashboard
{
	[Export]
	[Shared]
	public class DashboardViewModel : WasabiDocumentTabViewModel
	{
		private WalletManagerViewModel _walletManager;

		public DashboardViewModel() : base("Dashboard")
		{
			Shell = IoC.Get<IShell>();
			Global = Locator.Current.GetService<Global>();
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			OpenBrowserCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
		public IShell Shell { get; }
		public Global Global { get; }

		public List<NewsItem> News => Global.News.Items;

		private void InitializeWalletManagerVM()
		{
			_walletManager = Shell.Documents?.OfType<WalletManagerViewModel>()?.FirstOrDefault(x => x == _walletManager);

			if (_walletManager is null)
			{
				_walletManager = new WalletManagerViewModel();

				Shell.AddDocument(_walletManager);
			}

			Shell.Select(_walletManager);
		}

		public void RunGenerateWallet()
		{
			InitializeWalletManagerVM();
			_walletManager.SelectGenerateWallet();
		}

		public void RunRecoverWallet()
		{
			InitializeWalletManagerVM();
			_walletManager.SelectRecoverWallet();
		}

		public void RunConnectHardwareWallet()
		{
			InitializeWalletManagerVM();
			_walletManager.SelectConnectHardwareWallet();
		}
	}
}
