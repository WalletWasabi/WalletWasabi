using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletSettingsModel : ReactiveObject, IWalletSettingsModel
{
	private readonly KeyManager _keyManager;
	private bool _isDirty;

	[AutoNotify] private bool _isNewWallet;
	[AutoNotify] private bool _autoCoinjoin;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private Money _plebStopThreshold;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private double _coinjoinProbabilityDaily;
	[AutoNotify] private double _coinjoinProbabilityWeekly;
	[AutoNotify] private double _coinjoinProbabilityMonthly;

	public WalletSettingsModel(KeyManager keyManager, bool isNewWallet = false)
	{
		_keyManager = keyManager;

		_isNewWallet = isNewWallet;
		_isDirty = isNewWallet;

		_autoCoinjoin = _keyManager.AutoCoinJoin;
		_isCoinjoinProfileSelected = _keyManager.IsCoinjoinProfileSelected;
		_preferPsbtWorkflow = _keyManager.PreferPsbtWorkflow;
		_plebStopThreshold = _keyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		_anonScoreTarget = _keyManager.AnonScoreTarget;
		_redCoinIsolation = _keyManager.RedCoinIsolation;
		_coinjoinProbabilityDaily = _keyManager.CoinjoinProbabilityDaily;
		_coinjoinProbabilityWeekly = _keyManager.CoinjoinProbabilityWeekly;
		_coinjoinProbabilityMonthly = _keyManager.CoinjoinProbabilityMonthly;

		WalletName = _keyManager.WalletName;
		WalletType = WalletHelpers.GetType(_keyManager);

		this.WhenAnyValue(
			x => x.AutoCoinjoin,
			x => x.IsCoinjoinProfileSelected,
			x => x.PreferPsbtWorkflow,
			x => x.PlebStopThreshold,
			x => x.AnonScoreTarget,
			x => x.RedCoinIsolation)
			.Skip(1)
			.Do(_ => SetValues())
			.Subscribe();

		// This should go to the previous WhenAnyValue, it's just that it's not working for some reason.
		this.WhenAnyValue(
			x => x.CoinjoinProbabilityDaily,
			x => x.CoinjoinProbabilityWeekly,
			x => x.CoinjoinProbabilityMonthly)
			.Skip(1)
			.Do(_ => SetValues())
			.Subscribe();
	}

	public string WalletName { get; }

	public WalletType WalletType { get; }

	public void Save()
	{
		if (_isDirty)
		{
			_keyManager.ToFile();

			if (IsNewWallet)
			{
				Services.WalletManager.AddWallet(_keyManager);
				IsNewWallet = false;
			}

			_isDirty = false;
		}
	}

	private void SetValues()
	{
		_keyManager.AutoCoinJoin = AutoCoinjoin;
		_keyManager.IsCoinjoinProfileSelected = IsCoinjoinProfileSelected;
		_keyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_keyManager.PlebStopThreshold = PlebStopThreshold;
		_keyManager.AnonScoreTarget = AnonScoreTarget;
		_keyManager.RedCoinIsolation = RedCoinIsolation;
		_keyManager.CoinjoinProbabilityDaily = CoinjoinProbabilityDaily;
		_keyManager.CoinjoinProbabilityWeekly = CoinjoinProbabilityWeekly;
		_keyManager.CoinjoinProbabilityMonthly = CoinjoinProbabilityMonthly;

		_isDirty = true;
	}
}
