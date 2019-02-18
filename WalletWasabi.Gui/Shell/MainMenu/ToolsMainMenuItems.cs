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

		#region MainMenu

		[ExportMainMenuItem("Tools")]
		[DefaultOrder(1)]
		public IMenuItem Tools => _menuItemFactory.CreateHeaderMenuItem("Tools", null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup("Tools", "Wallet")]
		[DefaultOrder(0)]
		public object WalletGroup => null;

		[ExportMainMenuDefaultGroup("Tools", "Settings")]
		[DefaultOrder(1)]
		public object SettingsGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem("Tools", "Wallet")]
		[DefaultOrder(0)]
		[DefaultGroup("Wallet")]
		public IMenuItem WalletManager => _menuItemFactory.CreateCommandMenuItem("Tools.WalletManager");

		[ExportMainMenuItem("Tools", "EncryptionManager")]
		[DefaultOrder(1)]
		[DefaultGroup("Wallet")]
		public IMenuItem EncryptionManager => _menuItemFactory.CreateCommandMenuItem("Tools.EncryptionManager");

		[ExportMainMenuItem("Tools", "Settings")]
		[DefaultOrder(2)]
		[DefaultGroup("Settings")]
		public IMenuItem Settings => _menuItemFactory.CreateCommandMenuItem("Tools.Settings");

		#endregion MenuItem
	}
}
