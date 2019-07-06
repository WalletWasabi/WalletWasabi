using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	[Export]
	[Shared]
	internal class WalletManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		private LoadWalletViewModel LoadWalletViewModelDesktop { get; }
		private LoadWalletViewModel LoadWalletViewModelHardware { get; }

		[ImportingConstructor]
		public WalletManagerViewModel(AvaloniaGlobalComponent global) : base(global.Global, "Wallet Manager")
		{
			LoadWalletViewModelDesktop = new LoadWalletViewModel(this, LoadWalletType.Desktop);
			LoadWalletViewModelHardware = new LoadWalletViewModel(this, LoadWalletType.Hardware);

			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				LoadWalletViewModelDesktop,
				new LoadWalletViewModel(this, LoadWalletType.Password),
				LoadWalletViewModelHardware
			};

			SelectedCategory = Categories.FirstOrDefault();
			HardwareWalletRefreshCancel = new CancellationTokenSource();

			this.WhenAnyValue(x => x.SelectedCategory).Subscribe(category =>
			{
				category?.OnCategorySelected();

				CurrentView = category;
			});
		}

		public ObservableCollection<CategoryViewModel> Categories
		{
			get => _categories;
			set => this.RaiseAndSetIfChanged(ref _categories, value);
		}

		public CategoryViewModel SelectedCategory
		{
			get => _selectedCategory;
			set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
		}

		public void SelectGenerateWallet()
		{
			SelectedCategory = Categories.First(x => x is GenerateWalletViewModel);
		}

		public void SelectRecoverWallet()
		{
			SelectedCategory = Categories.First(x => x is RecoverWalletViewModel);
		}

		public void SelectLoadWallet()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel model && model.LoadWalletType == LoadWalletType.Desktop);
		}

		public void SelectTestPassword()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel model && model.LoadWalletType == LoadWalletType.Password);
		}

		public void SelectHardwareWallet()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel model && model.LoadWalletType == LoadWalletType.Hardware);
		}

		public ViewModelBase CurrentView
		{
			get => _currentView;
			set => this.RaiseAndSetIfChanged(ref _currentView, value);
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await RefreshHardwareWalletListAsync();
				HardwareWalletRefreshCancel?.Dispose();
				HardwareWalletRefreshCancel = null;
			});
		}

		private CancellationTokenSource HardwareWalletRefreshCancel { get; set; }
		private bool HwTabAutomaticallySelectedOnce { get; set; } = false;

		private async Task RefreshHardwareWalletListAsync()
		{
			try
			{
				int waitTime = 3000;
				while (!HardwareWalletRefreshCancel.IsCancellationRequested)
				{
					try
					{
						if (LoadWalletViewModelDesktop.IsWalletOpened || LoadWalletViewModelHardware.IsWalletOpened || HwiProcessManager.HwiPath is null)
						{
							continue; // Will wait 3sec, because of the finally.
						}

						var hwis = await HwiProcessManager.EnumerateAsync();
						LoadWalletViewModelHardware.TryRefreshHardwareWallets(hwis);

						if (hwis.Any())
						{
							waitTime = 7000;
							if (!HwTabAutomaticallySelectedOnce)
							{
								try
								{
									HwTabAutomaticallySelectedOnce = true;
									SelectHardwareWallet();
								}
								catch (Exception ex)
								{
									Logger.LogWarning<MainWindow>(ex);
								}
							}

							// Stop enumerating after you find one. Hardware wallets are acting up, sometimes fingerprint doesn't arrive for example.
							bool ledgerNotReady = hwis.Any(x => x.Type == HardwareWalletType.Ledger && !x.Ready);
							if (ledgerNotReady) // For Ledger you have to log into your "Bitcoin" account.
							{
								LoadWalletViewModelHardware.SetWarningMessage("Log into your Bitcoin account on your Ledger. If you're already logged in, log out and log in again.");
								continue;
							}
							else if (hwis.Any(x => x.Type == HardwareWalletType.Ledger && x.Ready))
							{
								LoadWalletViewModelHardware.SetWarningMessage("To have a smooth user experience consider turning off your Ledger screensaver.");
								break;
							}
							else
							{
								break;
							}
							//foreach (var hwi in hwis)
							//{
							//	// https://github.com/zkSNACKs/WalletWasabi/issues/1344#issuecomment-484607454
							//	if (hwi.Type == HardwareWalletType.Trezor // If Trezor Model T has passphrase set then user must keep confirming the enumerate command -> https://github.com/zkSNACKs/WalletWasabi/pull/1341#issuecomment-483916529
							//		|| hwi.Type == HardwareWalletType.Coldcard) //https://github.com/zkSNACKs/WalletWasabi/issues/1344#issuecomment-484691409
							//	{
							//		return;
							//	}
							//}
						}
						else
						{
							waitTime = 3000;
						}
					}
					catch (Exception ex)
					{
						LoadWalletViewModelHardware.SetValidationMessage(ex.ToTypeMessageString());
						Logger.LogWarning<WalletManagerViewModel>(ex);
					}
					finally
					{
						await Task.Delay(waitTime, HardwareWalletRefreshCancel.Token);
					}
				}
			}
			catch (TaskCanceledException ex)
			{
				Logger.LogTrace<WalletManagerViewModel>(ex);
			}
		}

		public override bool OnClose()
		{
			HardwareWalletRefreshCancel?.Cancel();
			return base.OnClose();
		}
	}
}
