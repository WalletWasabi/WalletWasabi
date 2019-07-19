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
		private IMenuItemFactory MenuItemFactory { get; }

		[ImportingConstructor]
		public ToolsMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			MenuItemFactory = menuItemFactory;
		}

		#region MainMenu

		[ExportMainMenuItem(nameof(Tools))]
		[DefaultOrder(1)]
		public IMenuItem Tools => MenuItemFactory.CreateHeaderMenuItem(nameof(Tools), null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup(nameof(Tools), "Managers")]
		[DefaultOrder(0)]
		public object ManagersGroup => null;

		[ExportMainMenuDefaultGroup(nameof(Tools), nameof(Settings))]
		[DefaultOrder(1)]
		public object SettingsGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem(nameof(Tools), "Wallet Manager")]
		[DefaultOrder(0)]
		[DefaultGroup("Managers")]
		public IMenuItem WalletManager => MenuItemFactory.CreateCommandMenuItem("Tools.WalletManager");

		[ExportMainMenuItem(nameof(Tools), nameof(Settings))]
		[DefaultOrder(1)]
		[DefaultGroup(nameof(Settings))]
		public IMenuItem Settings => MenuItemFactory.CreateCommandMenuItem("Tools.Settings");

		#endregion MenuItem
	}
}
