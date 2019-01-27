using AvalonStudio.MainMenu;
using AvalonStudio.Menus;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;

namespace WalletWasabi.Gui.Shell.MainMenu
{
	internal class ToolsMainMenuItems
	{
		private IMenuItemFactory _menuItemFactory;

		[ImportingConstructor]
		public ToolsMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			_menuItemFactory = menuItemFactory;
		}

		[ExportMainMenuItem("Tools")]
		[DefaultOrder(20)]
		public IMenuItem Tools => _menuItemFactory.CreateHeaderMenuItem("Tools", null);

		[ExportMainMenuDefaultGroup("Tools", "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuItem("Tools", "Wallet")]
		[DefaultOrder(0)]
		[DefaultGroup("Wallet")]
		public IMenuItem GenerateWallet => _menuItemFactory.CreateCommandMenuItem("Tools.WalletManager");

		[ExportMainMenuDefaultGroup("Tools", "Settings")]
		[DefaultOrder(1000)]
		public object SettingsGroup => null;

		[ExportMainMenuItem("Tools", "Settings")]
		[DefaultOrder(50)]
		[DefaultGroup("Settings")]
		public IMenuItem Settings => _menuItemFactory.CreateCommandMenuItem("Tools.Settings");
	}
}
