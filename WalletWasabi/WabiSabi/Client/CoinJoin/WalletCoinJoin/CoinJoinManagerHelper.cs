using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.WalletCoinJoin;

public class CoinJoinManagerHelper
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

	public static bool CanWalletCoinJoin(IWallet wallet, bool walletBlockedByUi)
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

	public static async Task<bool> ShouldWalletStartCoinJoinAsync(IWallet wallet, bool stopWhenAllMixed, bool isOverridePlebStop, IEnumerable<SmartCoin> coinCandidates)
	{
		// If payments are batched we always mix.
		if (wallet.BatchedPayments.AreTherePendingPayments)
		{
			return true;
		}

		// The wallet is already private.
		if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			if (stopWhenAllMixed)
			{
				return false;
			}
		}

		// Wallet total balance is lower then the PlebStop threshold. If the user not overrides that, we won't mix.
		if (wallet.IsUnderPlebStop)
		{
			if (!isOverridePlebStop)
			{
				return false;
			}
		}

		if (coinCandidates.All(x => !x.Confirmed || x.IsPrivate(wallet.AnonScoreTarget)) && stopWhenAllMixed)
		{
			return false;
		}

		return true;
	}
}
