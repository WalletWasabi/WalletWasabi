using NBitcoin;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class WalletSettingsModel : IWalletSettingsModel
{
	private readonly Wallet _wallet;
	private bool _batchChanges;

	public WalletSettingsModel(Wallet wallet)
	{
		_wallet = wallet;

		this.WhenAnyValue(
			x => x.AutoCoinjoin,
			x => x.IsCoinjoinProfileSelected,
			x => x.PreferPsbtWorkflow,
			x => x.PlebStopThreshold,
			x => x.AnonScoreTarget,
			x => x.RedCoinIsolation,
			x => x.FeeRateMedianTimeFrameHours)
			.Skip(1)
			.Do(_ => Save())
			.Subscribe();

		AutoCoinjoin = _wallet.KeyManager.AutoCoinJoin;
		IsCoinjoinProfileSelected = _wallet.KeyManager.IsCoinjoinProfileSelected;
		PreferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		PlebStopThreshold = wallet.KeyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		AnonScoreTarget = _wallet.KeyManager.AnonScoreTarget;
		RedCoinIsolation = _wallet.KeyManager.RedCoinIsolation;
		FeeRateMedianTimeFrameHours = _wallet.KeyManager.FeeRateMedianTimeFrameHours;
	}

	public bool AutoCoinjoin { get; set; }

	public bool IsCoinjoinProfileSelected { get; set; }

	public bool PreferPsbtWorkflow { get; set; }

	public Money PlebStopThreshold { get; set; }

	public int AnonScoreTarget { get; set; }

	public bool RedCoinIsolation { get; set; }

	public int FeeRateMedianTimeFrameHours { get; set; }

	/// <summary>
	/// Prevents the automatic persistence of Wallet Settings when individual properties change.
	/// This is useful when you want to change several properties, but only persist the settings once
	/// </summary>
	/// <returns>An IDisposable object that, when disposed, returns the WalletSettings back to normal operation (i.e save on each individual property change)</returns>
	public IDisposable BatchChanges()
	{
		_batchChanges = true;
		return Disposable.Create(() =>
		{
			_batchChanges = false;
			Save();
		});
	}

	private void Save()
	{
		_wallet.KeyManager.AutoCoinJoin = AutoCoinjoin;
		_wallet.KeyManager.IsCoinjoinProfileSelected = IsCoinjoinProfileSelected;
		_wallet.KeyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_wallet.KeyManager.PlebStopThreshold = PlebStopThreshold;
		_wallet.KeyManager.SetAnonScoreTarget(AnonScoreTarget, false);
		_wallet.KeyManager.RedCoinIsolation = RedCoinIsolation;
		_wallet.KeyManager.SetFeeRateMedianTimeFrame(FeeRateMedianTimeFrameHours, false);

		if (!_batchChanges)
		{
			_wallet.KeyManager.ToFile();
		}
	}
}
