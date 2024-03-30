using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using static WalletWasabi.WabiSabi.Client.CoinJoinManager;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager;

public static class CoinJoinManagerHelper
{
	public static IEnumerable<RoundState> GetRoundsForCoinJoin(IEnumerable<RoundState> allRounds)
	{
		return allRounds.Where(round => CanStartCoinJoin(round));
	}

	public static bool CanStartCoinJoin(RoundState roundState)
	{
		if (roundState.Phase != Backend.Rounds.Phase.InputRegistration)
		{
			return false;
		}

		if (roundState.IsBlame)
		{
			return false;
		}

		if (roundState.InputRegistrationEnd - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
		{
			// We do join in the last minute.
			return false;
		}

		if (roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min < Money.Coins(0.0001m))
		{
			return false;
		}

		var roundParameters = roundState.CoinjoinState.Parameters;

		if (!roundParameters.AllowedInputTypes.Contains(ScriptType.P2WPKH) || !roundParameters.AllowedOutputTypes.Contains(ScriptType.P2WPKH))
		{
			// Skipping the round since it doesn't support P2WPKH inputs and outputs.
			return false;
		}

		return true;
	}

	public static async Task<IEnumerable<IWallet>> GetWalletsForCoinJoinAsync(IEnumerable<IWallet> allWallets)
	{
		var results = await Task.WhenAll(allWallets.Select(
			async wallet => (
				CanCoinJoin: await CanWalletCoinJoinAsync(wallet, false, false).ConfigureAwait(false),
				Wallet: wallet))
			).ConfigureAwait(false);

		return results.Where(wallet => wallet.CanCoinJoin).Select(wallet => wallet.Wallet);
	}

	public static async Task<bool> CanWalletCoinJoinAsync(IWallet wallet, bool walletBlockedByUi, bool overridePlebStop)
	{
		// CoinJoin blocked by the UI. User is in action.
		if (walletBlockedByUi)
		{
			return false;
		}

		// Wallet has no KeyChain.
		if (wallet.KeyChain is null)
		{
			return false;
		}

		return true;
	}

	public static IEnumerable<RoundState> GetRoundsForWallet(IWallet wallet, IEnumerable<RoundState> rounds, Dictionary<TimeSpan, FeeRate> coinJoinFeeRateMedians)
	{
		var roundCandidates = rounds.Where(round => IsRoundForWallet(wallet, round, coinJoinFeeRateMedians));
		return roundCandidates;
	}

	public static bool IsRoundForWallet(IWallet wallet, RoundState round, Dictionary<TimeSpan, FeeRate> coinJoinFeeRateMedians)
	{
		var roundParameters = round.CoinjoinState.Parameters;
		if (!IsRoundEconomic(roundParameters.MiningFeeRate, coinJoinFeeRateMedians, wallet.FeeRateMedianTimeFrame))
		{
			return false;
		}

		return true;
	}

	internal static bool IsRoundEconomic(FeeRate roundFeeRate, Dictionary<TimeSpan, FeeRate> coinJoinFeeRateMedians, TimeSpan feeRateMedianTimeFrame)
	{
		if (feeRateMedianTimeFrame == default)
		{
			return true;
		}

		if (coinJoinFeeRateMedians.ContainsKey(feeRateMedianTimeFrame))
		{
			// Round is not economic if any TimeFrame lower than FeeRateMedianTimeFrame has a FeeRate lower than current round's FeeRate.
			// 0.5 satoshi difference is allowable, to avoid rounding errors.
			return coinJoinFeeRateMedians
				.Where(x => x.Key <= feeRateMedianTimeFrame)
				.All(lowerTimeFrame => roundFeeRate.SatoshiPerByte <= lowerTimeFrame.Value.SatoshiPerByte + 0.5m);
		}

		throw new InvalidOperationException($"Could not find median fee rate for time frame: {feeRateMedianTimeFrame}.");
	}

	public static void ProcessUserInput(IWallet wallet, WalletCoinJoinState walletCoinJoinState, StartCoinJoinCommand startCoinJoinCommand)
	{
		if (walletCoinJoinState.IsCoinJoining)
		{
			// No effect.
			return;
		}

		if (walletCoinJoinState.IsCoinJoining && userPressedStop)
		{
			walletCoinJoinState.IsStopTriggered = true;
			return;
		}

		if (!walletCoinJoinState.IsCoinJoining && userPressedStart)
		{
			walletCoinJoinState.IsStartTriggered = true;
			return;
		}
	}

	public static async Task<bool> ShouldWalletStartCoinJoinAsync(IWallet wallet, WalletCoinJoinState walletCoinJoinState)
	{
		if (walletCoinJoinState.IsCoinJoining)
		{
			// Wallet already coinjoins.
			return false;
		}

		// If payments are batched we always mix.
		if (wallet.BatchedPayments.AreTherePendingPayments)
		{
			return true;
		}

		// The wallet is already private.
		if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			if (walletCoinJoinState.StopWhenAllMixed)
			{
				return false;
			}
		}

		// Wallet total balance is lower then the PlebStop threshold. If the user not overrides that, we won't mix.
		if (wallet.IsUnderPlebStop)
		{
			if (!walletCoinJoinState.IsOverridePlebStop)
			{
				return false;
			}
		}

		if (wallet.IsAutoCoinJoin)
		{
			if (walletCoinJoinState.IsStopTriggered)
			{
				// User paused.
				return false;
			}
		}

		if (!wallet.IsAutoCoinJoin)
		{
			// User started the mix.
			if (walletCoinJoinState.IsStartTriggered)
			{
				return false;
			}
		}

		return true;
	}
}
