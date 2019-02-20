using AvalonStudio.MainMenu;
using AvalonStudio.Menus;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;

namespace WalletWasabi.Gui.Shell.MainMenu
{
	internal class FileMainMenuItems
	{
		private IMenuItemFactory MenuItemFactory { get; }

		[ImportingConstructor]
		public FileMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			MenuItemFactory = menuItemFactory;
		}

		[ExportMainMenuItem("File")]
		[DefaultOrder(0)]
		public IMenuItem File => MenuItemFactory.CreateHeaderMenuItem("File", null);

		[ExportMainMenuDefaultGroup("File", "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuItem("File", "Generate Wallet")]
		[DefaultOrder(1)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => MenuItemFactory.CreateCommandMenuItem("File.GenerateWallet");

		[ExportMainMenuItem("File", "Recover Wallet")]
		[DefaultOrder(2)]
		[DefaultGroup("Wallet")]
		public IMenuItem Recover => MenuItemFactory.CreateCommandMenuItem("File.RecoverWallet");

		[ExportMainMenuItem("File", "Load Wallet")]
		[DefaultOrder(3)]
		[DefaultGroup("Wallet")]
		public IMenuItem LoadWallet => MenuItemFactory.CreateCommandMenuItem("File.LoadWallet");

		[ExportMainMenuDefaultGroup("File", "Disk")]
		[DefaultOrder(10)]
		public object DiskGroup => null;

		[ExportMainMenuItem("File", "Open")]
		[DefaultOrder(11)]
		public IMenuItem Open => MenuItemFactory.CreateHeaderMenuItem("Open", null);

		[ExportMainMenuItem("File", "Open", "Data Folder")]
		[DefaultOrder(111)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenDataFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.DataFolder");

		[ExportMainMenuItem("File", "Open", "Wallets Folder")]
		[DefaultOrder(112)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenWalletsFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.WalletsFolder");

		[ExportMainMenuItem("File", "Open", "Log File")]
		[DefaultOrder(113)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.LogFile");

		[ExportMainMenuItem("File", "Open", "Tor Log File")]
		[DefaultOrder(114)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenTorLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.TorLogFile");

		[ExportMainMenuItem("File", "Open", "Config File")]
		[DefaultOrder(115)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenConfigFile => MenuItemFactory.CreateCommandMenuItem("File.Open.ConfigFile");

		[ExportMainMenuDefaultGroup("File", "Exit")]
		[DefaultOrder(1000)]
		public object ExitGroup => null;

		[ExportMainMenuItem("File", "Exit")]
		[DefaultOrder(1001)]
		[DefaultGroup("Exit")]
		public IMenuItem Exit => MenuItemFactory.CreateCommandMenuItem("File.Exit");
	}
}
