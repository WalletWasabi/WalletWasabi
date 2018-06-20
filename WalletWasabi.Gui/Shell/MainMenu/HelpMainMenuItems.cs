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
		private IMenuItemFactory _menuItemFactory;

		[ImportingConstructor]
		public HelpMainMenuItems(IMenuItemFactory menuItemFactory)
		{
			_menuItemFactory = menuItemFactory;
		}

		[ExportMainMenuItem("Help")]
		[DefaultOrder(40)]
		public IMenuItem Tools => _menuItemFactory.CreateHeaderMenuItem("Help", null);

		[ExportMainMenuDefaultGroup("Help", "About")]
		[DefaultOrder(0)]
		public object AboutGroup => null;

		[ExportMainMenuItem("Help", "About")]
		[DefaultOrder(0)]
		[DefaultGroup("About")]
		public IMenuItem GenerateWallet => _menuItemFactory.CreateCommandMenuItem("Help.About");

		[ExportMainMenuDefaultGroup("Help", "Support")]
		[DefaultOrder(100)]
		public object SupportGroup => null;

		[ExportMainMenuItem("Help", "CustomerSupport")]
		[DefaultOrder(0)]
		[DefaultGroup("Support")]
		public IMenuItem CustomerSupport => _menuItemFactory.CreateCommandMenuItem("Help.CustomerSupport");

		[ExportMainMenuItem("Help", "ReportBug")]
		[DefaultOrder(1)]
		[DefaultGroup("Support")]
		public IMenuItem ReportBug => _menuItemFactory.CreateCommandMenuItem("Help.ReportBug");

		[ExportMainMenuDefaultGroup("Help", "Legal")]
		[DefaultOrder(200)]
		public object LegalGroup => null;

		[ExportMainMenuItem("Help", "PrivacyPolicy")]
		[DefaultOrder(0)]
		[DefaultGroup("Legal")]
		public IMenuItem PrivacyPolicy => _menuItemFactory.CreateCommandMenuItem("Help.PrivacyPolicy");

		[ExportMainMenuItem("Help", "TermsAndConditions")]
		[DefaultOrder(1)]
		[DefaultGroup("Legal")]
		public IMenuItem TermsAndConditions => _menuItemFactory.CreateCommandMenuItem("Help.TermsAndConditions");
	}
}
