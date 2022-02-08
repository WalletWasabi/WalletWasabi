using NBitcoin;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class RoundParameters
{
	public RoundParameters(
		WabiSabiConfig wabiSabiConfig,
		Network network,
		WasabiRandom random,
		FeeRate feeRate,
		CoordinationFeeRate coordinationFeeRate)
	{
		Network = network;
		Random = random;
		FeeRate = feeRate;
		CoordinationFeeRate = coordinationFeeRate;

		MaxInputCountByRound = wabiSabiConfig.MaxInputCountByRound;
		MinInputCountByRound = wabiSabiConfig.MinInputCountByRound;
		MinRegistrableAmount = wabiSabiConfig.MinRegistrableAmount;
		MaxRegistrableAmount = wabiSabiConfig.MaxRegistrableAmount;

		// Note that input registration timeouts can be modified runtime.
		StandardInputRegistrationTimeout = wabiSabiConfig.StandardInputRegistrationTimeout;
		ConnectionConfirmationTimeout = wabiSabiConfig.ConnectionConfirmationTimeout;
		OutputRegistrationTimeout = wabiSabiConfig.OutputRegistrationTimeout;
		TransactionSigningTimeout = wabiSabiConfig.TransactionSigningTimeout;
		BlameInputRegistrationTimeout = wabiSabiConfig.BlameInputRegistrationTimeout;
	}

	public WasabiRandom Random { get; }
	public FeeRate FeeRate { get; }
	public CoordinationFeeRate CoordinationFeeRate { get; }
	public Network Network { get; }
	public int MinInputCountByRound { get; }
	public int MaxInputCountByRound { get; }
	public Money MinRegistrableAmount { get; }
	public Money MaxRegistrableAmount { get; }
	public TimeSpan StandardInputRegistrationTimeout { get; }
	public TimeSpan ConnectionConfirmationTimeout { get; }
	public TimeSpan OutputRegistrationTimeout { get; }
	public TimeSpan TransactionSigningTimeout { get; }
	public TimeSpan BlameInputRegistrationTimeout { get; }
}
