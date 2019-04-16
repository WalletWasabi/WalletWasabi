using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class WalletManagerViewModel : WasabiDocumentTabViewModel
	{
		private ObservableCollection<CategoryViewModel> _categories;
		private CategoryViewModel _selectedCategory;
		private ViewModelBase _currentView;
		private Task DetectHardwareWalletTask { get; set; }

		public WalletManagerViewModel() : base("Wallet Manager")
		{
			Categories = new ObservableCollection<CategoryViewModel>
			{
				new GenerateWalletViewModel(this),
				new RecoverWalletViewModel(this),
				new LoadWalletViewModel(this, LoadWalletType.Desktop),
				new LoadWalletViewModel(this, LoadWalletType.Password),
				new LoadWalletViewModel(this, LoadWalletType.Hardware)
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
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Desktop));
		}

		public void SelectTestPassword()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Password));
		}

		public void SelectHardwareWallet()
		{
			SelectedCategory = Categories.First(x => x is LoadWalletViewModel && (((LoadWalletViewModel)x).LoadWalletType == LoadWalletType.Hardware));
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
				DetectHardwareWalletTask = RefreshHardwareWalletListAsync();
				await RefreshHardwareWalletListAsync();
				HardwareWalletRefreshCancel?.Dispose();
			});
		}

		private List<HardwareWalletInfo> LastHardwareWalletEnumeration { get; set; }
		private CancellationTokenSource HardwareWalletRefreshCancel { get; }

		private async Task RefreshHardwareWalletListAsync()
		{
			while (!HardwareWalletRefreshCancel.IsCancellationRequested)
			{
				try
				{
					var hwis = await HwiProcessManager.EnumerateAsync();
					var hwlist = hwis.ToList();

					if (hwis.Any())
					{
						var alltypesunique = hwis.Count() == hwis.Select(x => x.Type).ToHashSet().Count();

						//foreach (HardwareWalletInfo hwi in hwis)
						//{
						//	if (alltypesunique)
						//	{
						//		Wallets.Add(hwi.Type.ToString());
						//	}
						//	else
						//	{
						//		Wallets.Add($"{hwi.Type}-{hwi.Fingerprint}");
						//	}
						//}

						//SelectedWallet = Wallets.FirstOrDefault();
						//SetWalletStates();
						//break;
					}

					await Task.Delay(3000, HardwareWalletRefreshCancel.Token);
				}
				catch (TaskCanceledException)
				{
				}
				catch (Exception ex)
				{
					//SetWarningMessage(ex.ToTypeMessageString());
					//Logger.LogError<LoadWalletViewModel>(ex);
				}
				finally
				{
					//IsHwWalletSearchTextVisible = false;
				}
			}
		}

		public override bool OnClose()
		{
			HardwareWalletRefreshCancel?.Cancel();
			return base.OnClose();
		}
	}
}
