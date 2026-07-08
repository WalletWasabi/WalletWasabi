using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Logging;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using ScriptType = WalletWasabi.Fluent.Models.Wallets.ScriptType;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[AppLifetime]
[NavigationMetaData(
    Title = "Wallet Settings",
    Caption = "Display wallet settings",
    IconName = "nav_wallet_24_regular",
    Order = 2,
    Category = "Wallet",
    Keywords = new[] { "Wallet", "Settings", },
    NavBarPosition = NavBarPosition.None,
    NavigationTarget = NavigationTarget.DialogScreen,
    Searchable = false)]
public partial class WalletSettingsViewModel : RoutableViewModel
{
    private readonly IWalletModel _wallet;
    [AutoNotify] private bool _preferPsbtWorkflow;
    [AutoNotify] private string _walletName;
    [AutoNotify] private int _selectedTab;
    [AutoNotify] private ScriptType _defaultReceiveScriptType;
    [AutoNotify] private bool _isSegWitDefaultReceiveScriptType;
    [AutoNotify] private WalletWasabi.Models.PreferredScriptPubKeyType _changeScriptPubKeyType;
    [AutoNotify] private WalletWasabi.Models.SendWorkflow _defaultSendWorkflow;
    [AutoNotify] private bool _isAutomaticDefaultSendWorkflow;

    public WalletSettingsViewModel(UiContext uiContext, IWalletModel walletModel) : base(uiContext)
    {
        _wallet = walletModel;
        _walletName = walletModel.Name;
        _preferPsbtWorkflow = walletModel.Settings.PreferPsbtWorkflow;
        _selectedTab = 0;
        IsHardwareWallet = walletModel.IsHardwareWallet;
        IsWatchOnly = walletModel.IsWatchOnlyWallet;

        this.ValidateProperty(
            x => x.WalletName,
            errors =>
            {
                if (_wallet.Name == WalletName)
                {
                    return;
                }

                if (UiContext.WalletRepository.ValidateWalletName(WalletName) is { } error)
                {
                    errors.Add(error.Severity, error.Message);
                }
            });

        SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
        var canSave = this.WhenAnyValue(x => x.WalletName, x => x.Validations,
            (name, validations) => !string.IsNullOrWhiteSpace(name) && !validations.Any);

        NextCommand = ReactiveCommand.Create(() =>
        {
            if (_wallet.Name != WalletName)
            {
                try
                {
                    _wallet.Rename(WalletName);
                }
                catch
                {
                    WalletName = _wallet.Name;
                    UiContext.Navigate().To().ShowErrorDialog(
                        $"The wallet cannot be renamed to {WalletName}",
                        "Invalid name",
                        "Cannot rename the wallet",
                        NavigationTarget.CompactDialogScreen);
                    return;
                }
            }

            _wallet.Settings.Save();
            Navigate().Back();
        }, canSave);

        _defaultReceiveScriptType = walletModel.Settings.DefaultReceiveScriptType;
        this.WhenAnyValue(x => x.DefaultReceiveScriptType)
            .Subscribe(value => IsSegWitDefaultReceiveScriptType = value == ScriptType.SegWit);

        _changeScriptPubKeyType = walletModel.Settings.ChangeScriptPubKeyType switch
        {
            PreferredScriptPubKeyType.Specified s => s.ScriptType switch
            {
                ScriptPubKeyType.TaprootBIP86 => PreferredScriptPubKeyType.Specified.Taproot,
                ScriptPubKeyType.Segwit => PreferredScriptPubKeyType.Specified.SegWit,
                _ => throw new ArgumentOutOfRangeException()
            },
            _ => walletModel.Settings.ChangeScriptPubKeyType
        };

        if (walletModel.IsTrezorCoinJoinWallet
            && _changeScriptPubKeyType is not PreferredScriptPubKeyType.Specified { ScriptType: ScriptPubKeyType.Segwit })
        {
            // SegWit is the only valid choice here (taproot keys of this wallet belong to the SLIP-25
            // coinjoin account); coerce so the selector does not show an empty value.
            _changeScriptPubKeyType = PreferredScriptPubKeyType.Specified.SegWit;
            walletModel.Settings.ChangeScriptPubKeyType = _changeScriptPubKeyType;
            walletModel.Settings.Save();
        }

        DefaultSendWorkflow = walletModel.Settings.DefaultSendWorkflow;
        this.WhenAnyValue(x => x.DefaultSendWorkflow)
            .Subscribe(value => IsAutomaticDefaultSendWorkflow = value == SendWorkflow.Automatic);

        WalletCoinJoinSettings = new WalletCoinJoinSettingsViewModel(UiContext, walletModel);

        VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To().WalletVerifyRecoveryWords(walletModel));

        // A Trezor watch-only wallet imported without coinjoin can opt in later. The device shows the new
        // coinjoin account for confirmation, then the wallet restarts so the coinjoin services pick it up.
        CanEnableCoinjoin = walletModel.CanEnableCoinjoin;
        EnableCoinjoinCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(3));
                await walletModel.EnableCoinjoinAsync(cts.Token);

                // The output provider reads the wallet's supported script types at construction, so restart to pick up the coinjoin account.
                UiContext.Navigate(MetaData.NavigationTarget).Clear();
                AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                await ShowErrorAsync("Enable coinjoin", ex.ToUserFriendlyString(), "Could not enable coinjoin.");
            }
        });

        ResyncWalletCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var heightToResync = await UiContext.Navigate().To().ResyncWallet(walletModel.GetWalletStats().BirthHeight).GetResultAsync();
            if (heightToResync is not null)
            {
                walletModel.Settings.RescanWallet((uint)heightToResync);
                UiContext.Navigate(MetaData.NavigationTarget).Clear();
                AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true);
            }
        });

        this.WhenAnyValue(x => x.DefaultSendWorkflow)
            .Skip(1)
            .Subscribe(value =>
            {
                walletModel.Settings.DefaultSendWorkflow = value;
                walletModel.Settings.Save();
            });

        this.WhenAnyValue(x => x.DefaultReceiveScriptType)
            .Skip(1)
            .Subscribe(value =>
            {
                walletModel.Settings.DefaultReceiveScriptType = value;
                walletModel.Settings.Save();
            });

        this.WhenAnyValue(x => x.ChangeScriptPubKeyType)
            .Skip(1)
            .Subscribe(value =>
            {
                walletModel.Settings.ChangeScriptPubKeyType = value;
                walletModel.Settings.Save();
            });

        this.WhenAnyValue(x => x.PreferPsbtWorkflow)
            .Skip(1)
            .Subscribe(value =>
            {
                walletModel.Settings.PreferPsbtWorkflow = value;
                walletModel.Settings.Save();
            });
    }

    public bool IsHardwareWallet { get; }
    public bool IsWatchOnly { get; }
    public bool SeveralReceivingScriptTypes => _wallet.SeveralReceivingScriptTypes;
    public bool IsDefaultSendWorkflowSettingVisible => !(IsWatchOnly || IsHardwareWallet);

    // Taproot receive addresses of a Trezor coinjoin wallet come from the SLIP-25 coinjoin account:
    // deposits to them are eligible for coinjoin right away, without a hop through the segwit account.
    public IEnumerable<ScriptType> ReceiveScriptTypes { get; } = [ScriptType.SegWit, ScriptType.Taproot];

    // Taproot change of a Trezor coinjoin wallet would land in the SLIP-25 account, which cannot sign regular transactions.
    public IEnumerable<PreferredScriptPubKeyType> ChangeScriptPubKeyTypes => _wallet.IsTrezorCoinJoinWallet
        ? [PreferredScriptPubKeyType.Specified.SegWit]
        :
        [
            PreferredScriptPubKeyType.Unspecified.Instance,
            PreferredScriptPubKeyType.Specified.SegWit,
            PreferredScriptPubKeyType.Specified.Taproot
        ];

    public IEnumerable<SendWorkflow> SendWorkflows { get; } = Enum.GetValues<SendWorkflow>();

    public WalletCoinJoinSettingsViewModel WalletCoinJoinSettings { get; private set; }
    public ICommand VerifyRecoveryWordsCommand { get; }
    public ICommand ResyncWalletCommand { get; }
    public bool CanEnableCoinjoin { get; }
    public ICommand EnableCoinjoinCommand { get; }

    protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
    {
        base.OnNavigatedTo(isInHistory, disposables);

        WalletName = _wallet.Name;

        WalletCoinJoinSettings.ManuallyUpdateOutputWalletList();
    }
}
