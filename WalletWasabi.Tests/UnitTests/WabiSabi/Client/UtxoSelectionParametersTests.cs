using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="UtxoSelectionParameters"/>.
/// </summary>
public class UtxoSelectionParametersTests
{
	private const long MinAllowedSatoshis = 5_000;
	private const long MaxAllowedSatoshis = 100_000_000;

	// What spending a P2WPKH input costs at the mining fee rate of the round.
	private const long SegwitInputFeeSatoshis = 6_900;

	private static readonly FeeRate MiningFeeRate = new(100m);

	/// <summary>
	/// The reason the wallet gives for not registering a coin is the one the coordinator would reject it with.
	/// </summary>
	/// <remarks>The smallest amount the round accepts is uneconomical here: the fee of the input is higher.</remarks>
	[Theory]
	[InlineData(ScriptPubKeyType.Segwit, 10_000, null)]
	[InlineData(ScriptPubKeyType.Segwit, MaxAllowedSatoshis, null)]
	[InlineData(ScriptPubKeyType.Segwit, MaxAllowedSatoshis + 1, WabiSabiProtocolErrorCode.TooMuchFunds)]
	[InlineData(ScriptPubKeyType.Segwit, MinAllowedSatoshis - 1, WabiSabiProtocolErrorCode.NotEnoughFunds)]
	[InlineData(ScriptPubKeyType.Segwit, MinAllowedSatoshis, WabiSabiProtocolErrorCode.UneconomicalInput)]
	[InlineData(ScriptPubKeyType.Segwit, SegwitInputFeeSatoshis, WabiSabiProtocolErrorCode.UneconomicalInput)]
	[InlineData(ScriptPubKeyType.Segwit, SegwitInputFeeSatoshis + 1, null)]
	[InlineData(ScriptPubKeyType.TaprootBIP86, 10_000, WabiSabiProtocolErrorCode.ScriptNotAllowed)]
	[InlineData(ScriptPubKeyType.TaprootBIP86, MinAllowedSatoshis - 1, WabiSabiProtocolErrorCode.ScriptNotAllowed)]
	public void IneligibilityIsWhatTheCoordinatorAnswers(ScriptPubKeyType scriptPubKeyType, long satoshis, WabiSabiProtocolErrorCode? expectedError)
	{
		var roundParameters = CreateSegwitOnlyRoundParameters();
		var keyManager = ServiceFactory.CreateKeyManager("", isTaprootAllowed: true);
		var coin = BitcoinFactory.CreateSmartCoin(keyManager.GetNextReceiveKey(LabelsArray.Empty, scriptPubKeyType), Money.Satoshis(satoshis));

		var utxoSelectionParameters = UtxoSelectionParameters.FromRoundParameters(roundParameters, [ScriptType.P2WPKH, ScriptType.Taproot]);
		var ineligibility = utxoSelectionParameters.GetIneligibility(coin);

		Assert.Equal(expectedError, GetCoordinatorAnswer(roundParameters, new KeyChain(keyManager, ""), coin));
		Assert.Equal(expectedError, ToProtocolError(ineligibility));
		Assert.Equal(expectedError is null, utxoSelectionParameters.Allows(coin));
	}

	/// <summary>
	/// The reason carries what the user needs to know to understand it.
	/// </summary>
	[Fact]
	public void IneligibilityTellsTheLimitThatWasNotMet()
	{
		var keyManager = ServiceFactory.CreateKeyManager("");
		var utxoSelectionParameters = UtxoSelectionParameters.FromRoundParameters(CreateSegwitOnlyRoundParameters(), [ScriptType.P2WPKH, ScriptType.Taproot]);

		CoinJoinIneligibility? GetIneligibility(long satoshis) =>
			utxoSelectionParameters.GetIneligibility(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(keyManager), Money.Satoshis(satoshis)));

		Assert.Equal(new CoinJoinIneligibility.AmountTooLow(Money.Satoshis(MinAllowedSatoshis)), GetIneligibility(MinAllowedSatoshis - 1));
		Assert.Equal(new CoinJoinIneligibility.AmountTooHigh(Money.Satoshis(MaxAllowedSatoshis)), GetIneligibility(MaxAllowedSatoshis + 1));
		Assert.Equal(new CoinJoinIneligibility.Uneconomical(MiningFeeRate), GetIneligibility(SegwitInputFeeSatoshis));
		Assert.Null(GetIneligibility(SegwitInputFeeSatoshis + 1));
	}

	private static RoundParameters CreateSegwitOnlyRoundParameters() =>
		WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig
		{
			MinRegistrableAmount = Money.Satoshis(MinAllowedSatoshis),
			MaxRegistrableAmount = Money.Satoshis(MaxAllowedSatoshis),
			AllowP2trInputs = false
		}) with
		{
			MiningFeeRate = MiningFeeRate
		};

	/// <returns>The error the coordinator rejects the registration of the coin with, or <c>null</c> if it accepts the coin.</returns>
	private static WabiSabiProtocolErrorCode? GetCoordinatorAnswer(RoundParameters roundParameters, IKeyChain keyChain, SmartCoin coin)
	{
		var commitmentData = new CoinJoinInputCommitmentData(roundParameters.CoordinationIdentifier, uint256.One);
		var ownershipProof = keyChain.GetOwnershipProof(coin, commitmentData);

		try
		{
			new ConstructionState(roundParameters).AddInput(coin.Coin, ownershipProof, commitmentData);
			return null;
		}
		catch (WabiSabiProtocolException ex)
		{
			return ex.ErrorCode;
		}
	}

	private static WabiSabiProtocolErrorCode? ToProtocolError(CoinJoinIneligibility? ineligibility) =>
		ineligibility switch
		{
			null => null,
			CoinJoinIneligibility.ScriptTypeNotAllowed => WabiSabiProtocolErrorCode.ScriptNotAllowed,
			CoinJoinIneligibility.AmountTooLow => WabiSabiProtocolErrorCode.NotEnoughFunds,
			CoinJoinIneligibility.AmountTooHigh => WabiSabiProtocolErrorCode.TooMuchFunds,
			CoinJoinIneligibility.Uneconomical => WabiSabiProtocolErrorCode.UneconomicalInput,
			_ => throw new ArgumentOutOfRangeException(nameof(ineligibility), ineligibility, "Unknown reason.")
		};
}
