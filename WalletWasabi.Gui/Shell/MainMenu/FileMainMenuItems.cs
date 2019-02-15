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

		[ExportMainMenuDefaultGroup("File", "Disk")]
		[DefaultOrder(0)]
		public object DiskGroup => null;

		[ExportMainMenuItem("File", "Open")]
		[DefaultOrder(0)]
		public IMenuItem Open => MenuItemFactory.CreateHeaderMenuItem("Open", null);

		[ExportMainMenuItem("File", "Open", "Data Folder")]
		[DefaultOrder(1)]
		[DefaultGroup("Disk")]
		public IMenuItem OpenDataFolder => MenuItemFactory.CreateCommandMenuItem("File.Open.DataFolder");

		[ExportMainMenuDefaultGroup("File", "Wallet")]
		[DefaultOrder(1)]
		public object WalletGroup => null;

		[ExportMainMenuItem("File", "Generate Wallet")]
		[DefaultOrder(10)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => MenuItemFactory.CreateCommandMenuItem("File.GenerateWallet");

		[ExportMainMenuItem("File", "Recover Wallet")]
		[DefaultOrder(11)]
		[DefaultGroup("Wallet")]
		public IMenuItem Recover => MenuItemFactory.CreateCommandMenuItem("File.RecoverWallet");

		[ExportMainMenuItem("File", "Load Wallet")]
		[DefaultOrder(12)]
		[DefaultGroup("Wallet")]
		public IMenuItem LoadWallet => MenuItemFactory.CreateCommandMenuItem("File.LoadWallet");

		[ExportMainMenuDefaultGroup("File", "Exit")]
		[DefaultOrder(1000)]
		public object ExitGroup => null;

		[ExportMainMenuItem("File", "Exit")]
		[DefaultOrder(0)]
		[DefaultGroup("Exit")]
		public IMenuItem Exit => MenuItemFactory.CreateCommandMenuItem("File.Exit");
	}
}
