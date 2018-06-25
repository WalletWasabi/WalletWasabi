using AvalonStudio.MainMenu;
using AvalonStudio.Menus;
using System.Composition;

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

		[ExportMainMenuItem("File", "Recover Wallet")]
		[DefaultOrder(10)]
		[DefaultGroup("Wallet")]
		public IMenuItem Recover => _menuItemFactory.CreateCommandMenuItem("File.RecoverWallet");

		[ExportMainMenuItem("File", "Load Wallet")]
		[DefaultOrder(20)]
		[DefaultGroup("Wallet")]
		public IMenuItem LoadWallet => _menuItemFactory.CreateCommandMenuItem("File.LoadWallet");

		[ExportMainMenuDefaultGroup("File", "Exit")]
		[DefaultOrder(1000)]
		public object ExitGroup => null;

		[ExportMainMenuItem("File", "Exit")]
		[DefaultOrder(0)]
		[DefaultGroup("Exit")]
		public IMenuItem Exit => _menuItemFactory.CreateCommandMenuItem("File.Exit");
	}
}
