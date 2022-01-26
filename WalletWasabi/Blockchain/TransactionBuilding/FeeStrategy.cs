using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class FeeStrategy
{
	private int? _target;
	private FeeRate? _rate;

	private FeeStrategy(FeeStrategyType type, int? confirmationTarget, FeeRate? feeRate)
	{
		Type = type;
		if (type == FeeStrategyType.Rate)
		{
			if (confirmationTarget is not null)
			{
				throw new ArgumentException($"{nameof(confirmationTarget)} must be null.");
			}
			feeRate = Guard.NotNull(nameof(feeRate), feeRate);
			if (feeRate < new FeeRate(1m))
			{
				throw new ArgumentOutOfRangeException(nameof(feeRate), feeRate, "Cannot be less than 1 sat/vByte.");
			}
			_rate = feeRate;
		}
		else if (type == FeeStrategyType.Target)
		{
			if (feeRate is not null)
			{
				throw new ArgumentException($"{nameof(feeRate)} must be null.");
			}

			_target = Guard.InRangeAndNotNull(nameof(confirmationTarget), Guard.NotNull(nameof(confirmationTarget), confirmationTarget).Value, Constants.TwentyMinutesConfirmationTarget, Constants.SevenDaysConfirmationTarget);
		}
		else
		{
			throw new NotSupportedException(type.ToString());
		}
	}

	public FeeStrategyType Type { get; }

	public int? Target
	{
		get
		{
			if (Type != FeeStrategyType.Target)
			{
				throw new NotSupportedException($"Cannot get {nameof(Target)} with {nameof(FeeStrategyType)} {Type}.");
			}
			return _target;
		}
	}

	public FeeRate? Rate
	{
		get
		{
			if (Type != FeeStrategyType.Rate)
			{
				throw new NotSupportedException($"Cannot get {nameof(Rate)} with {nameof(FeeStrategyType)} {Type}.");
			}
			return _rate;
		}
	}

	public static FeeStrategy TwentyMinutesConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.TwentyMinutesConfirmationTarget);
	public static FeeStrategy OneDayConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.OneDayConfirmationTarget);
	public static FeeStrategy SevenDaysConfirmationTargetStrategy { get; } = CreateFromConfirmationTarget(Constants.SevenDaysConfirmationTarget);

	public static FeeStrategy CreateFromConfirmationTarget(int confirmationTarget) => new(FeeStrategyType.Target, confirmationTarget: confirmationTarget, feeRate: null);

	public static FeeStrategy CreateFromFeeRate(FeeRate feeRate) => new(FeeStrategyType.Rate, confirmationTarget: null, feeRate: feeRate);
}
