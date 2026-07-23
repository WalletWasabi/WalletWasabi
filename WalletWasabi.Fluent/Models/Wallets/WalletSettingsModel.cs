using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AppLifetime]
public partial class WalletSettingsModel : ReactiveObject
{
	private readonly IServices _services;
	private readonly KeyManager _keyManager;
	private bool _isDirty;

	[AutoNotify] private bool _isNewWallet;
	[AutoNotify] private bool _autoCoinjoin;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private Money _plebStopThreshold;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private int _trezorCoinjoinMaxRounds;
	[AutoNotify] private decimal _trezorCoinjoinMaxMiningFeeRate;
	[AutoNotify] private bool _nonPrivateCoinIsolation;
	[AutoNotify] private WalletId? _outputWalletId;
	[AutoNotify] private ScriptType _defaultReceiveScriptType;
	[AutoNotify] private PreferredScriptPubKeyType _changeScriptPubKeyType;
	[AutoNotify] private SendWorkflow _defaultSendWorkflow;

	public WalletSettingsModel(IServices services, KeyManager keyManager, bool isNewWallet = false, bool isCoinJoinPaused = false)
	{
		_services = services;
		_keyManager = keyManager;

		_isNewWallet = isNewWallet;
		_isDirty = isNewWallet;
		IsCoinJoinPaused = isCoinJoinPaused;

		_autoCoinjoin = _keyManager.AutoCoinJoin;
		_preferPsbtWorkflow = _keyManager.PreferPsbtWorkflow;
		_plebStopThreshold = _keyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		_anonScoreTarget = _keyManager.AnonScoreTarget;
		_trezorCoinjoinMaxRounds = _keyManager.TrezorCoinjoinMaxRounds;
		_trezorCoinjoinMaxMiningFeeRate = _keyManager.TrezorCoinjoinMaxMiningFeeRate;
		_nonPrivateCoinIsolation = _keyManager.NonPrivateCoinIsolation;

		if (!isNewWallet)
		{
			_outputWalletId = services.GetWalletByName(_keyManager.WalletName).WalletId;
		}

		_defaultReceiveScriptType = ScriptType.FromEnum(_keyManager.DefaultReceiveScriptType);
		_changeScriptPubKeyType = _keyManager.ChangeScriptPubKeyType;
		_defaultSendWorkflow = _keyManager.DefaultSendWorkflow;

		WalletType = WalletHelpers.GetType(_keyManager);

		this.WhenAnyValue(
				x => x.AutoCoinjoin,
				x => x.PreferPsbtWorkflow,
				x => x.PlebStopThreshold,
				x => x.AnonScoreTarget,
				x => x.NonPrivateCoinIsolation)
			.Skip(1)
			.Do(_ => SetValues())
			.Subscribe();

		this.WhenAnyValue(
				x => x.TrezorCoinjoinMaxRounds,
				x => x.TrezorCoinjoinMaxMiningFeeRate)
			.Skip(1)
			.Do(_ => SetValues())
			.Subscribe();

		this.WhenAnyValue(
				x => x.DefaultSendWorkflow,
				x => x.DefaultReceiveScriptType,
				x => x.ChangeScriptPubKeyType)
			.Do(_ => SetValues())
			.Subscribe();
	}

	public WalletType WalletType { get; }

	public bool IsTrezorCoinJoinWallet => _keyManager.IsTrezorCoinJoinWallet();

	public bool IsCoinJoinPaused { get; set; }

	/// <summary>
	/// Saves to current configuration to file.
	/// </summary>
	/// <returns>The unique ID of the wallet.</returns>
	public WalletId Save()
	{
		if (_isDirty)
		{
			_keyManager.ToFile();

			if (IsNewWallet)
			{
				_services.AddWallet(_keyManager);
				IsNewWallet = false;
				OutputWalletId = _services.GetWalletByName(_keyManager.WalletName).WalletId;
			}

			_isDirty = false;
		}

		return _services.GetWalletByName(_keyManager.WalletName).WalletId;
	}

	private void SetValues()
	{
		_keyManager.AutoCoinJoin = AutoCoinjoin;
		_keyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_keyManager.PlebStopThreshold = PlebStopThreshold;
		_keyManager.AnonScoreTarget = AnonScoreTarget;
		_keyManager.TrezorCoinjoinMaxRounds = TrezorCoinjoinMaxRounds;
		_keyManager.TrezorCoinjoinMaxMiningFeeRate = TrezorCoinjoinMaxMiningFeeRate;
		_keyManager.NonPrivateCoinIsolation = NonPrivateCoinIsolation;
		_keyManager.DefaultSendWorkflow = DefaultSendWorkflow;
		_keyManager.DefaultReceiveScriptType = ScriptType.ToScriptPubKeyType(DefaultReceiveScriptType);
		_keyManager.ChangeScriptPubKeyType = ChangeScriptPubKeyType;
		_isDirty = true;
	}

	public void RescanWallet(uint startingHeight = 0)
	{
		_keyManager.SetBestHeight(startingHeight + Constants.ResyncHeightMargin);
	}
}
