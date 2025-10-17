using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionFeeHelper
{
	private static readonly FeeRateEstimations TestNetFeeRateEstimations = new(
		new Dictionary<int, FeeRate>
		{
			[1] = new( 17m),
			[2] = new( 12m),
			[3] = new( 9m),
			[6] = new( 9m),
			[18] = new( 2m),
			[36] = new( 2m),
			[72] = new( 2m),
			[144] = new( 2m),
			[432] = new( 1m),
			[1008] = new( 1m)
		});

	public static async Task<FeeRateEstimations> GetFeeEstimatesWhenReadyAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		if (TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			return feeEstimates;
		}


		throw new InvalidOperationException("Couldn't get the fee estimations.");
	}

	public static async Task<TimeSpan?> EstimateConfirmationTimeAsync(FeeRateEstimations feeRateEstimations, Network network, SmartTransaction tx, CpfpInfoProvider cpfpInfoProvider, CancellationToken cancellationToken)
	{
		if (TryGetFeeEstimates(feeRateEstimations, network, out var feeEstimates) && feeEstimates.TryEstimateConfirmationTime(tx, out var estimate))
		{
			return estimate;
		}

		if (feeEstimates is null)
		{
			return null;
		}

		var availableCpfpInfo = await cpfpInfoProvider.GetCachedCpfpInfoAsync(cancellationToken).ConfigureAwait(false);
		if (availableCpfpInfo.FirstOrDefault(x => x.Transaction.GetHash() == tx.Transaction.GetHash()) is not { } entry)
		{
			return null;
		}

		var feeRate = new FeeRate(entry.CpfpInfo.EffectiveFeePerVSize);
		return feeEstimates.EstimateConfirmationTime(feeRate);;
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

	public static bool TryGetFeeEstimates(Wallet wallet, [NotNullWhen(true)] out FeeRateEstimations? estimates)
		=> TryGetFeeEstimates(wallet.FeeRateEstimations, wallet.Network, out estimates);

	public static bool TryGetFeeEstimates(FeeRateEstimations? feeRateEstimations, Network network, [NotNullWhen(true)] out FeeRateEstimations? estimates)
	{
		if (network == Network.TestNet)
		{
			estimates = TestNetFeeRateEstimations;
			return true;
		}
		if (feeRateEstimations is not null)
		{
			estimates = feeRateEstimations;
			return true;
		}

		estimates = null;
		return false;
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

	/// <summary>
	/// Seeks for the maximum possible fee rate that the transaction can pay.
	/// </summary>
	/// <remarks>The method does not throw any exception.</remarks>
	/// <remarks>Stores the found fee rate in the received <see cref="TransactionInfo"/> object. </remarks>
	/// <returns>True if the seeking was successful, False if not.</returns>
	public static async Task<bool> TrySetMaxFeeRateAsync(Wallet wallet, TransactionInfo info)
	{
		var maxFeeRate =
			await Task.Run(() =>
			{
				var found = FeeHelpers.TryGetMaxFeeRate(wallet, info.Destination, info.Amount, info.Recipient, info.FeeRate, info.Coins, info.SubtractFee, out var maxFeeRate);

				return found ? maxFeeRate! : new FeeRate(0m);
			});

		if (EnsureFeeRateIsPossible(wallet, maxFeeRate))
		{
			info.MaximumPossibleFeeRate = maxFeeRate;
			info.FeeRate = maxFeeRate;
			info.ConfirmationTimeSpan = TryEstimateConfirmationTime(wallet, maxFeeRate, out var estimate)
				? estimate.Value
				: TimeSpan.Zero;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Temporary solution for making sure if a fee rate can be found in the fee chart.
	/// It is needed otherwise we cannot predict the confirmation time and the fee chart would crash.
	/// TODO: Remove this hack when the issues mentioned above are fixed.
	/// </summary>
	private static bool EnsureFeeRateIsPossible(Wallet wallet, FeeRate feeRate)
	{
		if (!TryGetFeeEstimates(wallet, out var feeEstimates))
		{
			return false;
		}

		var feeChartViewModel = new FeeChartViewModel();
		feeChartViewModel.UpdateFeeEstimates(feeEstimates.WildEstimations);

		if (!feeChartViewModel.TryGetConfirmationTarget(feeRate, out var blockTarget))
		{
			return false;
		}

		var newFeeRate = new FeeRate(feeChartViewModel.GetSatoshiPerByte(blockTarget));
		return newFeeRate <= feeRate;
	}
}
