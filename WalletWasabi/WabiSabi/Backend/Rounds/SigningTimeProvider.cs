using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record SigningTimeProvider(
	uint MaxInternalInputCount,
	uint MaxInternalOutputCount,
	TimeSpan SigningOverhead,
	TimeSpan InternalInputSigningTime,
	TimeSpan ExternalInputSigningTime,
	TimeSpan InternalOutputSigningTime,
	TimeSpan ExternalOutputSigningTime)
{
	// The values were obtained based on benchmarking the transaction signing on Trezor
	public static SigningTimeProvider Trezor = new(CoinJoinCoinSelector.MaxInputsRegistrableByWallet, 10, TimeSpan.FromSeconds(1.98 + 5), TimeSpan.FromSeconds(0.44), TimeSpan.FromSeconds(0.09), TimeSpan.FromSeconds(0.15), TimeSpan.FromSeconds(0.04));

	public TimeSpan GetSigningTime(int inputCount, int outputCount)
	{
		return SigningOverhead + MaxInternalInputCount * InternalInputSigningTime + (inputCount - MaxInternalInputCount) * ExternalInputSigningTime + MaxInternalOutputCount * InternalOutputSigningTime + (outputCount - MaxInternalOutputCount) * ExternalOutputSigningTime;
	}
}
