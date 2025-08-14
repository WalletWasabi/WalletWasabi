using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class FeeStrategy
{
	public static readonly FeeRate MinimumFeeRate = Constants.MinRelayFeeRate;

	private int? _target;
	private FeeRate? _feeRate;

	private FeeStrategy(int confirmationTarget)
	{
		Type = FeeStrategyType.Target;
		_target = Guard.InRangeAndNotNull(nameof(confirmationTarget), confirmationTarget, smallest: Constants.TwentyMinutesConfirmationTarget, greatest: Constants.SevenDaysConfirmationTarget);
		_feeRate = null;
	}

	private FeeStrategy(FeeRate feeRate)
	{
		if (feeRate < MinimumFeeRate)
		{
			throw new ArgumentOutOfRangeException(nameof(feeRate), feeRate, $"Cannot be less than {MinimumFeeRate.SatoshiPerByte} sat/vByte.");
		}

		Type = FeeStrategyType.Rate;
		_target = null;
		_feeRate = feeRate;
	}

	public static FeeStrategy TwentyMinutesConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.TwentyMinutesConfirmationTarget);
	public static FeeStrategy OneDayConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.OneDayConfirmationTarget);
	public static FeeStrategy SevenDaysConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.SevenDaysConfirmationTarget);

	public FeeStrategyType Type { get; }

	public bool TryGetTarget([NotNullWhen(true)] out int? target)
	{
		if (Type == FeeStrategyType.Target)
		{
			target = _target!.Value;
			return true;
		}

		target = null;
		return false;
	}

	public bool TryGetFeeRate([NotNullWhen(true)] out FeeRate? rate)
	{
		if (Type == FeeStrategyType.Rate)
		{
			rate = _feeRate!;
			return true;
		}

		rate = null;
		return false;
	}

	public static FeeStrategy CreateFromConfirmationTarget(int confirmationTarget)
		=> new(confirmationTarget: confirmationTarget);

	public static FeeStrategy CreateFromFeeRate(FeeRate feeRate)
		=> new(feeRate: feeRate);

	public static FeeStrategy CreateFromFeeRate(decimal satoshiPerByte) => CreateFromFeeRate(new FeeRate(satoshiPerByte));
}
