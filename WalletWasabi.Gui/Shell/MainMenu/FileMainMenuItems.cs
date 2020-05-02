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
		[ImportingConstructor]
		public FileMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			MenuItemFactory = menuItemFactory;
		}

		private IMenuItemFactory MenuItemFactory { get; }

		#region MainMenu

		[ExportMainMenuItem("File")]
		[DefaultOrder(0)]
		public IMenuItem File => MenuItemFactory.CreateHeaderMenuItem("File", null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup("File", "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuDefaultGroup("File", "Disk")]
		[DefaultOrder(1)]
		public object DiskGroup => null;

		[ExportMainMenuDefaultGroup("File", "Security")]
		[DefaultOrder(2)]
		public object SecurityGroup => null;

		[ExportOnPlatform(osx: false)]
		[ExportMainMenuDefaultGroup("File", "System")]
		[DefaultOrder(3)]
		public object SystemGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem("File", "Generate Wallet")]
		[DefaultOrder(0)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => MenuItemFactory.CreateCommandMenuItem("File.GenerateWallet");

		[ExportMainMenuItem("File", "Recover Wallet")]
		[DefaultOrder(1)]
		[DefaultGroup("Wallet")]
		public IMenuItem Recover => MenuItemFactory.CreateCommandMenuItem("File.RecoverWallet");

		[ExportMainMenuItem("File", "Load Wallet")]
		[DefaultOrder(2)]
		[DefaultGroup("Wallet")]
		public IMenuItem LoadWallet => MenuItemFactory.CreateCommandMenuItem("File.LoadWallet");

		[ExportMainMenuItem("File", "Open")]
		[DefaultOrder(3)]
		public IMenuItem Open => MenuItemFactory.CreateHeaderMenuItem("Open", null);

		[ExportMainMenuItem("File", "LockScreen")]
		[DefaultOrder(4)]
		[DefaultGroup("Security")]
		public IMenuItem LockScreen => MenuItemFactory.CreateCommandMenuItem("File.LockScreen");

		[ExportMainMenuItem("File", "Exit")]
		[DefaultOrder(5)]
		[DefaultGroup("System")]
		public IMenuItem Exit => MenuItemFactory.CreateCommandMenuItem("File.Exit");

		#endregion MenuItem

		#region SubMenuItem

		[ExportMainMenuItem("File", "Open", "Data Folder")]
		[DefaultOrder(0)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenDataFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.DataFolder");

		[ExportMainMenuItem("File", "Open", "Wallets Folder")]
		[DefaultOrder(1)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenWalletsFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.WalletsFolder");

		[ExportMainMenuItem("File", "Open", "Log File")]
		[DefaultOrder(2)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.LogFile");

		[ExportMainMenuItem("File", "Open", "Tor Log File")]
		[DefaultOrder(3)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenTorLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.TorLogFile");

		[ExportMainMenuItem("File", "Open", "Config File")]
		[DefaultOrder(4)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenConfigFile => MenuItemFactory.CreateCommandMenuItem("File.Open.ConfigFile");

		#endregion SubMenuItem
	}
}
