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
		private IMenuItemFactory _menuItemFactory;

		[ImportingConstructor]
		public FileMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			_menuItemFactory = menuItemFactory;
		}

		[ExportMainMenuItem("File")]
		[DefaultOrder(0)]
		public IMenuItem File => _menuItemFactory.CreateHeaderMenuItem("File", null);

		[ExportMainMenuDefaultGroup("File", "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuItem("File", "Generate Wallet")]
		[DefaultOrder(0)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => _menuItemFactory.CreateCommandMenuItem("File.GenerateWallet");

		[ExportMainMenuDefaultGroup("File", "Exit")]
		[DefaultOrder(1000)]
		public object ExitGroup => null;

		[ExportMainMenuItem("File", "Exit")]
		[DefaultOrder(0)]
		[DefaultGroup("Exit")]
		public IMenuItem Exit => _menuItemFactory.CreateCommandMenuItem("File.Exit");
	}
}
