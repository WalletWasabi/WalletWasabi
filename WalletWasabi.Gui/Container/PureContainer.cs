using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Wallets;
using WalletWasabi.Legal;

namespace WalletWasabi.Gui.Container
{
	public class PureContainer
	{
		private readonly WalletManager WalletManager;
		private readonly StatusBarViewModel StatusBarViewModel;
		private readonly WalletManagerViewModel WalletManagerViewModel;
		private readonly MainWindowViewModel MainWindowViewModel;

		public PureContainer(UiConfig uiConfig, LegalDocuments legalDocuments, WalletManager walletManager, StatusBarViewModel statusBarViewModel)
		{
			WalletManager = walletManager;
			UiConfig = uiConfig;
			LegalDocuments = legalDocuments;
			StatusBarViewModel = statusBarViewModel;
			WalletManagerViewModel = new WalletManagerViewModel(WalletManager);
			MainWindowViewModel = new MainWindowViewModel(WalletManager, UiConfig, StatusBarViewModel, WalletManagerViewModel);			
		}

		public UiConfig UiConfig { get; }
		public LegalDocuments LegalDocuments { get; }

		public MainWindowViewModel GetSingletonMainWindowViewModel()
		{
			return MainWindowViewModel;
		}
	}
}
