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

		#region MainMenu

		[ExportMainMenuItem(nameof(File))]
		[DefaultOrder(0)]
		public IMenuItem File => MenuItemFactory.CreateHeaderMenuItem(nameof(File), null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup(nameof(File), "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuDefaultGroup(nameof(File), "Disk")]
		[DefaultOrder(1)]
		public object DiskGroup => null;

		[ExportMainMenuDefaultGroup(nameof(File), nameof(Exit))]
		[DefaultOrder(2)]
		public object ExitGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem(nameof(File), "Generate Wallet")]
		[DefaultOrder(0)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => MenuItemFactory.CreateCommandMenuItem("File.GenerateWallet");

		[ExportMainMenuItem(nameof(File), "Recover Wallet")]
		[DefaultOrder(1)]
		[DefaultGroup("Wallet")]
		public IMenuItem Recover => MenuItemFactory.CreateCommandMenuItem("File.RecoverWallet");

		[ExportMainMenuItem(nameof(File), "Load Wallet")]
		[DefaultOrder(2)]
		[DefaultGroup("Wallet")]
		public IMenuItem LoadWallet => MenuItemFactory.CreateCommandMenuItem("File.LoadWallet");

		[ExportMainMenuItem(nameof(File), nameof(Open))]
		[DefaultOrder(3)]
		public IMenuItem Open => MenuItemFactory.CreateHeaderMenuItem(nameof(Open), null);

		[ExportMainMenuItem(nameof(File), nameof(Exit))]
		[DefaultOrder(4)]
		[DefaultGroup(nameof(Exit))]
		public IMenuItem Exit => MenuItemFactory.CreateCommandMenuItem("File.Exit");

		#endregion MenuItem

		#region SubMenuItem

		[ExportMainMenuItem(nameof(File), nameof(Open), "Data Folder")]
		[DefaultOrder(0)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenDataFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.DataFolder");

		[ExportMainMenuItem(nameof(File), nameof(Open), "Wallets Folder")]
		[DefaultOrder(1)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenWalletsFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.WalletsFolder");

		[ExportMainMenuItem(nameof(File), nameof(Open), "Log File")]
		[DefaultOrder(2)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.LogFile");

		[ExportMainMenuItem(nameof(File), nameof(Open), "Tor Log File")]
		[DefaultOrder(3)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenTorLogFile => MenuItemFactory.CreateCommandMenuItem("File.Open.TorLogFile");

		[ExportMainMenuItem(nameof(File), nameof(Open), "Config File")]
		[DefaultOrder(4)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenConfigFile => MenuItemFactory.CreateCommandMenuItem("File.Open.ConfigFile");

		#endregion SubMenuItem
	}
}
