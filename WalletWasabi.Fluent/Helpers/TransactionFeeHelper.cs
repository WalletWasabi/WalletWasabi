using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionFeeHelper
{
	private static readonly AllFeeEstimate TestNetFeeEstimates = new(
		EstimateSmartFeeMode.Conservative,
		new Dictionary<int, int>
		{
			[1] = 17,
			[2] = 12,
			[3] = 9,
			[6] = 9,
			[18] = 2,
			[36] = 2,
			[72] = 2,
			[144] = 2,
			[432] = 1,
			[1008] = 1
		},
		false);

	public static async Task<AllFeeEstimate> GetFeeEstimatesWhenReadyAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		var feeProvider = wallet.FeeProvider;

		bool RpcFeeProviderInError() => feeProvider.RpcFeeProvider?.InError ?? true;
		bool ThirdPartyFeeProviderInError() => feeProvider.ThirdPartyFeeProvider.InError;

		while (!RpcFeeProviderInError() || !ThirdPartyFeeProviderInError())
		{
			if (TryGetFeeEstimates(wallet, out var feeEstimates))
			{
				return feeEstimates;
			}

			await Task.Delay(100, cancellationToken);
		}

		throw new InvalidOperationException("Couldn't get the fee estimations.");
	}

	public static bool TryEstimateConfirmationTime(HybridFeeProvider feeProvider, Network network, SmartTransaction tx, [NotNullWhen(true)] out TimeSpan? estimate)
	{
		estimate = null;
		return TryGetFeeEstimates(feeProvider, network, out var feeEstimates) && feeEstimates.TryEstimateConfirmationTime(tx, out estimate);
	}

	public static bool TryEstimateConfirmationTime(Wallet wallet, SmartTransaction tx, [NotNullWhen(true)] out TimeSpan? estimate)
	{
		estimate = null;
		return TryGetFeeEstimates(wallet, out var feeEstimates) && feeEstimates.TryEstimateConfirmationTime(tx, out estimate);
	}

	public static bool TryEstimateConfirmationTime(Wallet wallet, FeeRate feeRate, [NotNullWhen(true)] out TimeSpan? estimate)
	{
		estimate = null;
		if (TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			estimate = feeEstimates.EstimateConfirmationTime(feeRate);
		}

		return estimate is not null;
	}

	public static bool TryGetFeeEstimates(Wallet wallet, [NotNullWhen(true)] out AllFeeEstimate? estimates)
		=> TryGetFeeEstimates(wallet.FeeProvider, wallet.Network, out estimates);

	public static bool TryGetFeeEstimates(HybridFeeProvider feeProvider, Network network, [NotNullWhen(true)] out AllFeeEstimate? estimates)
	{
		estimates = null;

		if (feeProvider.AllFeeEstimate is null)
		{
			return false;
		}

		estimates = network == Network.TestNet ? TestNetFeeEstimates : feeProvider.AllFeeEstimate;
		return true;
	}

	public static TimeSpan CalculateConfirmationTime(double targetBlock)
	{
		var timeInMinutes = Math.Ceiling(targetBlock) * 10;
		var time = TimeSpan.FromMinutes(timeInMinutes);

		// Format the timespan to only include the largest unit of time.
		// This is confirmation estimation so we can't be precise and we shouldn't give that impression that we can.
		if (time.TotalDays >= 1)
		{
			time = TimeSpan.FromDays(Math.Ceiling(time.TotalDays));
		}
		else if (time.TotalHours >= 1)
		{
			time = TimeSpan.FromHours(Math.Ceiling(time.TotalHours));
		}
		else if (time.TotalMinutes >= 1)
		{
			time = TimeSpan.FromMinutes(Math.Ceiling(time.TotalMinutes));
		}
		else if (time.TotalSeconds >= 1)
		{
			time = TimeSpan.FromSeconds(Math.Ceiling(time.TotalSeconds));
		}

		return time;
	}

	public static async Task<bool> TrySetMaxFeeRateAsync(Wallet wallet, TransactionInfo info)
	{
		var newInfo = info.Clone();
		var lastWrongFeeRate = new FeeRate(0m);
		var lastCorrectFeeRate = new FeeRate(0m);

		do
		{
			try
			{
				await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, newInfo, tryToSign: false));
				var increaseBy = lastWrongFeeRate.SatoshiPerByte == 0 ? newInfo.FeeRate.SatoshiPerByte : (lastWrongFeeRate.SatoshiPerByte - newInfo.FeeRate.SatoshiPerByte) / 2;
				lastCorrectFeeRate = newInfo.FeeRate;
				newInfo.FeeRate = new FeeRate(newInfo.FeeRate.SatoshiPerByte + increaseBy);
			}
			catch (Exception)
			{
				lastWrongFeeRate = newInfo.FeeRate;
				var decreaseBy = (newInfo.FeeRate.SatoshiPerByte - lastCorrectFeeRate.SatoshiPerByte) / 2;
				newInfo.FeeRate = new FeeRate(newInfo.FeeRate.SatoshiPerByte - decreaseBy);
			}
		} while (Math.Abs(lastWrongFeeRate.SatoshiPerByte - lastCorrectFeeRate.SatoshiPerByte) > 0.001m &&
		         !(lastWrongFeeRate.SatoshiPerByte < 1m && lastCorrectFeeRate.SatoshiPerByte < 1m));

		if (lastCorrectFeeRate.SatoshiPerByte >= 1m)
		{
			info.MaximumPossibleFeeRate = lastCorrectFeeRate;
			info.FeeRate = lastCorrectFeeRate;
			info.ConfirmationTimeSpan = TryEstimateConfirmationTime(wallet, lastCorrectFeeRate, out var estimate)
				? estimate.Value
				: TimeSpan.Zero;

			return true;
		}

		return false;
	}
}
