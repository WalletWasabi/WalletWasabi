using AvalonStudio.MainMenu;
using AvalonStudio.Menus;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;

namespace WalletWasabi.Gui.Shell.MainMenu
{
	internal class HelpMainMenuItems
	{
		private IMenuItemFactory MenuItemFactory { get; }

		[ImportingConstructor]
		public HelpMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			MenuItemFactory = menuItemFactory;
		}

		#region MainMenu

		[ExportMainMenuItem("Help")]
		[DefaultOrder(2)]
		public IMenuItem Tools => MenuItemFactory.CreateHeaderMenuItem("Help", null);

		#endregion MainMenu

		#region Group

		[ExportMainMenuDefaultGroup("Help", "About")]
		[DefaultOrder(0)]
		public object AboutGroup => null;

		[ExportMainMenuDefaultGroup("Help", "Support")]
		[DefaultOrder(1)]
		public object SupportGroup => null;

		[ExportMainMenuDefaultGroup("Help", "Legal")]
		[DefaultOrder(2)]
		public object LegalGroup => null;

		#endregion Group

		#region MenuItem

		[ExportMainMenuItem("Help", "About")]
		[DefaultOrder(0)]
		[DefaultGroup("About")]
		public IMenuItem About => MenuItemFactory.CreateCommandMenuItem("Help.About");

		[ExportMainMenuItem("Help", "Customer Support")]
		[DefaultOrder(1)]
		[DefaultGroup("Support")]
		public IMenuItem CustomerSupport => MenuItemFactory.CreateCommandMenuItem("Help.CustomerSupport");

		[ExportMainMenuItem("Help", "Report Bug")]
		[DefaultOrder(2)]
		[DefaultGroup("Support")]
		public IMenuItem ReportBug => MenuItemFactory.CreateCommandMenuItem("Help.ReportBug");

		[ExportMainMenuItem("Help", "Documentation")]
		[DefaultOrder(3)]
		[DefaultGroup("Support")]
		public IMenuItem Docs => MenuItemFactory.CreateCommandMenuItem("Help.Documentation");

		[ExportMainMenuItem("Help", "Legal Documents")]
		[DefaultOrder(4)]
		[DefaultGroup("Legal")]
		public IMenuItem LegalDocuments => MenuItemFactory.CreateCommandMenuItem("Help.LegalDocuments");

		#endregion MenuItem
	}
}
