using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionFeeHelper
{
	private static readonly Dictionary<int, int> TestNetFeeEstimates = new()
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
	};

	public static async Task<Dictionary<int, int>> GetFeeEstimatesWhenReadyAsync(Wallet wallet, CancellationToken cancellationToken)
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

	public static bool TryGetFeeEstimates(Wallet wallet, [NotNullWhen(true)] out Dictionary<int, int>? estimates)
	{
		estimates = null;

		if (wallet.FeeProvider.AllFeeEstimate is null)
		{
			return false;
		}

		estimates = wallet.Network == Network.TestNet ? TestNetFeeEstimates : wallet.FeeProvider.AllFeeEstimate.Estimations;
		return true;
	}

	public static bool AreTransactionFeesEqual(Wallet wallet)
	{
		if (!TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			return false;
		}

		var first = feeEstimates.First();
		var last = feeEstimates.Last();

		return first.Value == last.Value;
	}

	public static TimeSpan CalculateConfirmationTime(FeeRate feeRate, Wallet wallet)
	{
		if (!TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			return TimeSpan.Zero;
		}

		var feeChartViewModel = new FeeChartViewModel();
		feeChartViewModel.UpdateFeeEstimates(feeEstimates);

		return feeChartViewModel.TryGetConfirmationTarget(feeRate, out var target)
			? CalculateConfirmationTime(target)
			: TimeSpan.Zero;
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

	public static bool TryGetMaximumPossibleFeeRate(decimal percentageOfOverpayment, Wallet wallet, FeeRate currentFeeRate, out FeeRate maximumPossibleFeeRate)
	{
		maximumPossibleFeeRate = FeeRate.Zero;

		if (percentageOfOverpayment <= 0 || !TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			return false;
		}

		var maxPossibleFeeRateInSatoshiPerByte = (currentFeeRate.SatoshiPerByte / percentageOfOverpayment) * 100;
		maximumPossibleFeeRate = new FeeRate(maxPossibleFeeRateInSatoshiPerByte);

		var feeChartViewModel = new FeeChartViewModel();
		feeChartViewModel.UpdateFeeEstimates(feeEstimates);

		if (!feeChartViewModel.TryGetConfirmationTarget(maximumPossibleFeeRate, out var blockTarget))
		{
			return false;
		}

		var newFeeRate = new FeeRate(feeChartViewModel.GetSatoshiPerByte(blockTarget));
		return newFeeRate <= maximumPossibleFeeRate;
	}
}
