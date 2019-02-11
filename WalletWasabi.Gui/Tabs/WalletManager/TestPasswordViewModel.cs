using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class TestPasswordViewModel : CategoryViewModel
	{
		private ObservableCollection<string> _wallets;
		private string _selectedWallet;

		private WalletManagerViewModel Owner { get; }

		public TestPasswordViewModel(WalletManagerViewModel owner) : base("Test Password")
		{
			Owner = owner;
			_wallets = new ObservableCollection<string>();
		}

		public ObservableCollection<string> Wallets
		{
			get { return _wallets; }
			set { this.RaiseAndSetIfChanged(ref _wallets, value); }
		}

		public string SelectedWallet
		{
			get { return _selectedWallet; }
			set { this.RaiseAndSetIfChanged(ref _selectedWallet, value); }
		}

		public override void OnCategorySelected()
		{
			_wallets.Clear();

			if (!File.Exists(Global.WalletsDir))
			{
				Directory.CreateDirectory(Global.WalletsDir);
			}

			var directoryInfo = new DirectoryInfo(Global.WalletsDir);
			var walletFiles = directoryInfo.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
			foreach (var file in walletFiles)
			{
				_wallets.Add(Path.GetFileNameWithoutExtension(file.FullName));
			}

			if (_wallets.Any())
			{
				SelectedWallet = _wallets.First();
			}
		}
	}
}
