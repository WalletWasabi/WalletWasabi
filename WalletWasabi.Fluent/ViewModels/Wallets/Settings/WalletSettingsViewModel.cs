using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
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

    public WalletSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
    {
        UiContext = uiContext;
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

        DefaultReceiveScriptType = walletModel.Settings.DefaultReceiveScriptType;
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

        DefaultSendWorkflow = walletModel.Settings.DefaultSendWorkflow;
        this.WhenAnyValue(x => x.DefaultSendWorkflow)
            .Subscribe(value => IsAutomaticDefaultSendWorkflow = value == SendWorkflow.Automatic);

        WalletCoinJoinSettings = new WalletCoinJoinSettingsViewModel(UiContext, walletModel);

        VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To().WalletVerifyRecoveryWords(walletModel));

        ResyncWalletCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            int? heightToResync = await UiContext.Navigate().To().ResyncWallet().GetResultAsync();
            if (heightToResync is not null)
            {
                walletModel.Settings.RescanWallet((int)heightToResync);
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

    public IEnumerable<ScriptType> ReceiveScriptTypes { get; } = [ScriptType.SegWit, ScriptType.Taproot];
    public IEnumerable<PreferredScriptPubKeyType> ChangeScriptPubKeyTypes { get; } =
    [
        PreferredScriptPubKeyType.Unspecified.Instance,
        PreferredScriptPubKeyType.Specified.SegWit,
        PreferredScriptPubKeyType.Specified.Taproot
    ];

    public IEnumerable<SendWorkflow> SendWorkflows { get; } = Enum.GetValues<SendWorkflow>();

    public WalletCoinJoinSettingsViewModel WalletCoinJoinSettings { get; private set; }
    public ICommand VerifyRecoveryWordsCommand { get; }
    public ICommand ResyncWalletCommand { get; }

    protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
    {
        base.OnNavigatedTo(isInHistory, disposables);

        WalletName = _wallet.Name;

        WalletCoinJoinSettings.ManuallyUpdateOutputWalletList();
    }
}
