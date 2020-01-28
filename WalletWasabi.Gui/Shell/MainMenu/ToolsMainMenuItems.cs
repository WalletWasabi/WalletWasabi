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

		[ExportMainMenuItem("Tools")]
		[DefaultOrder(1)]
		public IMenuItem Tools => MenuItemFactory.CreateHeaderMenuItem("Tools", null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup("Tools", "Utilities")]
		[DefaultOrder(0)]
		public object UtilitiesGroup => null;

		[ExportMainMenuDefaultGroup("Tools", "Settings")]
		[DefaultOrder(10)]
		public object SettingsGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem("Tools", "Wallet Manager")]
		[DefaultOrder(1)]
		[DefaultGroup("Utilities")]
		public IMenuItem WalletManager => MenuItemFactory.CreateCommandMenuItem("Tools.WalletManager");

#if DEBUG

		[ExportMainMenuItem("Tools", "Dev Tools")]
		[DefaultOrder(10)]
		[DefaultGroup("Utilities")]
		public IMenuItem DevTools => MenuItemFactory.CreateCommandMenuItem("Tools.DevTools");
#endif

		[ExportMainMenuItem("Tools", "Transaction Broadcaster")]
		[DefaultOrder(2)]
		[DefaultGroup("Utilities")]
		public IMenuItem BroadcastTransaction => MenuItemFactory.CreateCommandMenuItem("Tools.BroadcastTransaction");

		[ExportMainMenuItem("Tools", "Settings")]
		[DefaultOrder(20)]
		[DefaultGroup("Settings")]
		public IMenuItem Settings => MenuItemFactory.CreateCommandMenuItem("Tools.Settings");

		#endregion MenuItem
	}
}
