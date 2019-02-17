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

		[ExportMainMenuItem("Help")]
		[DefaultOrder(40)]
		public IMenuItem Tools => MenuItemFactory.CreateHeaderMenuItem("Help", null);

		[ExportMainMenuDefaultGroup("Help", "About")]
		[DefaultOrder(0)]
		public object AboutGroup => null;

		[ExportMainMenuItem("Help", "About")]
		[DefaultOrder(0)]
		[DefaultGroup("About")]
		public IMenuItem About => MenuItemFactory.CreateCommandMenuItem("Help.About");

		[ExportMainMenuDefaultGroup("Help", "Support")]
		[DefaultOrder(100)]
		public object SupportGroup => null;

		[ExportMainMenuItem("Help", "CustomerSupport")]
		[DefaultOrder(0)]
		[DefaultGroup("Support")]
		public IMenuItem CustomerSupport => MenuItemFactory.CreateCommandMenuItem("Help.CustomerSupport");

		[ExportMainMenuItem("Help", "ReportBug")]
		[DefaultOrder(1)]
		[DefaultGroup("Support")]
		public IMenuItem ReportBug => MenuItemFactory.CreateCommandMenuItem("Help.ReportBug");

		[ExportMainMenuDefaultGroup("Help", "Legal")]
		[DefaultOrder(200)]
		public object LegalGroup => null;

		[ExportMainMenuItem("Help", "PrivacyPolicy")]
		[DefaultOrder(0)]
		[DefaultGroup("Legal")]
		public IMenuItem PrivacyPolicy => MenuItemFactory.CreateCommandMenuItem("Help.PrivacyPolicy");

		[ExportMainMenuItem("Help", "TermsAndConditions")]
		[DefaultOrder(1)]
		[DefaultGroup("Legal")]
		public IMenuItem TermsAndConditions => MenuItemFactory.CreateCommandMenuItem("Help.TermsAndConditions");

		[ExportMainMenuItem("Help", "LegalIssues")]
		[DefaultOrder(2)]
		[DefaultGroup("Legal")]
		public IMenuItem LegalIssues => MenuItemFactory.CreateCommandMenuItem("Help.LegalIssues");

#if DEBUG

		[ExportMainMenuItem("Help", "Dev Tools")]
		[DefaultOrder(300)]
		[DefaultGroup("About")]
		public IMenuItem DevTools => MenuItemFactory.CreateCommandMenuItem("Help.DevTools");

#endif
	}
}
