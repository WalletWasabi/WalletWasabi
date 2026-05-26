using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.CoinControl;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.ReleaseHighlights;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Scheme;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.CoinJoinPayment;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Fluent.ViewModels.Wallets.Settings;
using WalletWasabi.Wallets;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public delegate Task<string?> ShowQrCodeCameraDialog(RoutableViewModel routableViewModel, Network network);

public partial class FluentNavigate
{
	public FluentNavigate(UiContext uiContext)
	{
		UiContext = uiContext;
	}

	public UiContext UiContext { get; }

	private async Task<string?> ShowQrCodeCameraDialogAsync(RoutableViewModel routableViewModel, Network network)
	{
		return await routableViewModel.Navigate().To().ShowQrCameraDialog(network).GetResultAsync();
	}

	public FluentDialog<string?> ShowQrCameraDialog(Network network, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ShowQrCameraDialogViewModel(UiContext, network);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<string?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void TransactionDetails(IWalletModel wallet, TransactionModel model, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new TransactionDetailsViewModel(UiContext, wallet, model), navigationMode);
	}

	public FluentDialog<LabelsArray?> AddressLabelEdit(IWalletModel wallet, IAddress address, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new AddressLabelEditViewModel(UiContext, wallet, address);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<LabelsArray?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void Loading(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new LoadingViewModel(UiContext, wallet), navigationMode);
	}

	public void WalletVerifyRecoveryWords(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletVerifyRecoveryWordsViewModel(UiContext, wallet), navigationMode);
	}

	public FluentDialog<bool> PasswordAuthDialog(IWalletModel wallet, string continueText = "Continue", NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new PasswordAuthDialogViewModel(UiContext, wallet, continueText);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<bool>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void RecoverWallet(WalletCreationOptions.RecoverWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new RecoverWalletViewModel(UiContext, options), navigationMode);
	}

	public void SpeedUpTransactionDialog(IWalletModel wallet, SpeedupTransaction speedupTransaction, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new SpeedUpTransactionDialogViewModel(UiContext, wallet, speedupTransaction), navigationMode);
	}

	public void NavBarItem(INavBarItem item, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new NavBarItemViewModel(UiContext, item), navigationMode);
	}

	public void About(bool navigateBack = false, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new AboutViewModel(UiContext, navigateBack), navigationMode);
	}

	public FluentDialog<bool> ShowErrorDialog(string message, string title, string caption, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ShowErrorDialogViewModel(UiContext, message, title, caption);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<bool>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void WalletInfo(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletInfoViewModel(UiContext, wallet), navigationMode);
	}

	public void WalletCoins(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletCoinsViewModel(UiContext, wallet), navigationMode);
	}

	public void Broadcaster(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new BroadcasterViewModel(UiContext), navigationMode);
	}

	public void OpenTorLogs(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new OpenTorLogsViewModel(UiContext), navigationMode);
	}

	public void WalletStats(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletStatsViewModel(UiContext, wallet), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> AddWalletPage(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new AddWalletPageViewModel(UiContext);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void BugReportLink(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new BugReportLinkViewModel(UiContext), navigationMode);
	}

	public FluentDialog<bool> HardwareWalletAuthDialog(IHardwareWalletModel wallet, TransactionAuthorizationInfo transactionAuthorizationInfo, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new HardwareWalletAuthDialogViewModel(UiContext, wallet, transactionAuthorizationInfo);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<bool>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public FluentDialog<IEnumerable<SmartCoin>> PrivacyControl(Wallet wallet, SendFlowModel sendFlow, TransactionInfo transactionInfo, IEnumerable<SmartCoin>? usedCoins, bool isSilent, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new PrivacyControlViewModel(UiContext, wallet, sendFlow, transactionInfo, usedCoins, isSilent);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<IEnumerable<SmartCoin>>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void PrivacyRing(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new PrivacyRingViewModel(UiContext, wallet), navigationMode);
	}

	public FluentDialog<IEnumerable<SmartCoin>> SelectCoinsDialog(IWalletModel wallet, IList<CoinModel> selectedCoins, SendFlowModel sendFlow, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new SelectCoinsDialogViewModel(UiContext, wallet, selectedCoins, sendFlow);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<IEnumerable<SmartCoin>>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void BitcoinTabSettings(ApplicationSettings settings, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new BitcoinTabSettingsViewModel(UiContext, settings), navigationMode);
	}

	public void TransactionPreview(IWalletModel walletModel, SendFlowModel sendFlow, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new TransactionPreviewViewModel(UiContext, walletModel, sendFlow), navigationMode);
	}

	public void Login(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new LoginViewModel(UiContext, wallet), navigationMode);
	}

	public FluentDialog<string?> CreatePasswordDialog(string title, string caption = "", bool enableEmpty = true, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new CreatePasswordDialogViewModel(UiContext, title, caption, enableEmpty);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<string?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void Receive(IWalletModel wallet, Models.Wallets.ScriptType scriptType, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope
		UiContext.Navigate(navigationTarget).To(new ReceiveViewModel(UiContext, wallet, scriptType), navigationMode);
#pragma warning restore CA2000 // Dispose objects before losing scope
	}

	public void WalletNamePage(WalletCreationOptions options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletNamePageViewModel(UiContext, options), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> ReleaseHighlightsDialog(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ReleaseHighlightsDialogViewModel(UiContext);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public FluentDialog<FeeRate> CustomFeeRateDialog(TransactionInfo transactionInfo, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new CustomFeeRateDialogViewModel(UiContext, transactionInfo);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<FeeRate>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void DetectedHardwareWallet(WalletCreationOptions.ConnectToHardwareWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new DetectedHardwareWalletViewModel(UiContext, options), navigationMode);
	}

	public void SendSuccess(SmartTransaction finalTransaction, string? title = null, string? caption = null, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new SendSuccessViewModel(UiContext, finalTransaction, title, caption), navigationMode);
	}

	public FluentDialog<(int MinGapLimit, uint BirthHeight)?> AdvancedRecoveryOptions(int minGapLimit, uint genesisHeight, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new AdvancedRecoveryOptionsViewModel(UiContext, minGapLimit, genesisHeight);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<(int, uint)?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void RecoverMultiShareWallet(WalletCreationOptions.RecoverWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new RecoverMultiShareWalletViewModel(UiContext, options), navigationMode);
	}

	public void CancelTransactionDialog(IWalletModel wallet, CancellingTransaction cancellingTransaction, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new CancelTransactionDialogViewModel(UiContext, wallet, cancellingTransaction), navigationMode);
	}

	public void RecoveryWords(WalletCreationOptions.AddNewWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new RecoveryWordsViewModel(UiContext, options), navigationMode);
	}

	public void OpenDataFolder(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new OpenDataFolderViewModel(UiContext), navigationMode);
	}

	public void OpenWalletsFolder(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new OpenWalletsFolderViewModel(UiContext), navigationMode);
	}

	public void SchemeConsole(Client.Scheme schemeInterpreter, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new SchemeConsoleViewModel(UiContext, schemeInterpreter), navigationMode);
	}

	public FluentDialog<int?> ResyncWallet(uint birthHeight, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ResyncWalletViewModel(UiContext, birthHeight);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<int?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void GeneralSettingsTab(ApplicationSettings settings, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new GeneralSettingsTabViewModel(UiContext, settings), navigationMode);
	}

	public void ConnectionsSettingsTab(ApplicationSettings settings, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ConnectionsSettingsTabViewModel(UiContext, settings), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> SettingsPage(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new SettingsPageViewModel(UiContext);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void BroadcastTransaction(SmartTransaction transaction, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new BroadcastTransactionViewModel(UiContext, transaction), navigationMode);
	}

	public void ConnectHardwareWallet(WalletCreationOptions.ConnectToHardwareWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ConnectHardwareWalletViewModel(UiContext, options), navigationMode);
	}

	public void ConfirmMultiShare(WalletCreationOptions.AddNewWallet options, Dictionary<int, List<RecoveryWordViewModel>> wordsDictionary, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ConfirmMultiShareViewModel(UiContext, options, wordsDictionary), navigationMode);
	}

	public FluentDialog<IEnumerable<SmartCoin>> ManualControlDialog(IWalletModel walletModel, Wallet wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ManualControlDialogViewModel(UiContext, walletModel, wallet);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<IEnumerable<SmartCoin>>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void AddCoinJoinPayment(IWalletModel walletModel, Wallet wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var viewModel = new AddCoinJoinPaymentViewModel(UiContext, walletModel, wallet, ShowQrCodeCameraDialogAsync);
		UiContext.Navigate(navigationTarget).To(viewModel, navigationMode);
	}

	public void MultiShare(WalletCreationOptions.AddNewWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new MultiShareViewModel(UiContext, options), navigationMode);
	}

	public FluentDialog<bool> ConfirmHideAddress(LabelsArray labels, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ConfirmHideAddressViewModel(UiContext, labels);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<bool>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void OpenConfigFile(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new OpenConfigFileViewModel(UiContext), navigationMode);
	}

	public void ReceiveAddress(IWalletModel wallet, IAddress model, bool isAutoCopyEnabled, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ReceiveAddressViewModel(UiContext, wallet, model, isAutoCopyEnabled), navigationMode);
	}

	public void ShuttingDown(ApplicationViewModel applicationViewModel, bool restart, NavigationTarget navigationTarget = NavigationTarget.CompactDialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ShuttingDownViewModel(UiContext, applicationViewModel, restart), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> CoinJoinPayments(IWalletModel walletModel, Wallet wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new CoinJoinPaymentsViewModel(UiContext, walletModel, wallet);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void CoinJoinDetails(IWalletModel wallet, TransactionModel transaction, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new CoinJoinDetailsViewModel(UiContext, wallet, transaction), navigationMode);
	}

	public void OpenLogs(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new OpenLogsViewModel(UiContext), navigationMode);
	}

	public void Send(IWalletModel walletModel, SendFlowModel parameters, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var viewModel = new SendViewModel(UiContext, walletModel, parameters, ShowQrCodeCameraDialogAsync);
		UiContext.Navigate(navigationTarget).To(viewModel, navigationMode);
	}

	public void PrivacyMode(ApplicationSettings applicationSettings, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new PrivacyModeViewModel(UiContext, applicationSettings), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> ExcludedCoins(IWalletModel wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new ExcludedCoinsViewModel(UiContext, wallet);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void AddedWalletPage(WalletSettingsModel walletSettings, WalletCreationOptions options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new AddedWalletPageViewModel(UiContext, walletSettings, options), navigationMode);
	}

	public void UserSupport(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new UserSupportViewModel(UiContext), navigationMode);
	}

	public FluentDialog<bool> NewCoordinatorConfirmationDialog(CoordinatorConnectionString coordinatorConnection, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new NewCoordinatorConfirmationDialogViewModel(UiContext, coordinatorConnection);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<bool>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void ReceiveAddresses(IWalletModel wallet, Models.Wallets.ScriptType scriptType, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ReceiveAddressesViewModel(UiContext, wallet, scriptType), navigationMode);
	}

	public void WalletCoinJoinSettings(IWalletModel walletModel, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletCoinJoinSettingsViewModel(UiContext, walletModel), navigationMode);
	}

	public void MultiShareOptions(WalletCreationOptions.AddNewWallet options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new MultiShareOptionsViewModel(UiContext, options), navigationMode);
	}

	public void FindCoordinatorLink(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new FindCoordinatorLinkViewModel(UiContext), navigationMode);
	}

	public void WalletSettings(IWalletModel walletModel, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletSettingsViewModel(UiContext, walletModel), navigationMode);
	}

	public void ConfirmRecoveryWords(WalletCreationOptions.AddNewWallet options, List<RecoveryWordViewModel> words, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new ConfirmRecoveryWordsViewModel(UiContext, options, words), navigationMode);
	}

	public void DocsLink(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new DocsLinkViewModel(UiContext), navigationMode);
	}

	public FluentDialog<System.Reactive.Unit> WelcomePage(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new WelcomePageViewModel(UiContext);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<System.Reactive.Unit>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public FluentDialog<SmartTransaction?> LoadTransaction(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new LoadTransactionViewModel(UiContext);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<SmartTransaction?>(target.NavigateDialogAsync(dialog, navigationMode));
	}

	public void Wallet(IWalletModel walletModel, Wallet wallet, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletViewModel(UiContext, walletModel, wallet), navigationMode);
	}

	public void Success(NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new SuccessViewModel(UiContext), navigationMode);
	}

	public void CoinJoinsDetails(IWalletModel wallet, TransactionModel transaction, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new CoinJoinsDetailsViewModel(UiContext, wallet, transaction), navigationMode);
	}

	public void CoordinatorTabSettings(ApplicationSettings settings, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new CoordinatorTabSettingsViewModel(UiContext, settings), navigationMode);
	}

	public void WalletBackupType(WalletCreationOptions options, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		UiContext.Navigate(navigationTarget).To(new WalletBackupTypeViewModel(UiContext, options), navigationMode);
	}

	public FluentDialog<FeeRate> SendFee(Wallet wallet, TransactionInfo transactionInfo, bool isSilent, NavigationTarget navigationTarget = NavigationTarget.DialogScreen, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialog = new SendFeeViewModel(UiContext, wallet, transactionInfo, isSilent);
		var target = UiContext.Navigate(navigationTarget);
		target.To(dialog, navigationMode);

		return new FluentDialog<FeeRate>(target.NavigateDialogAsync(dialog, navigationMode));
	}

}

