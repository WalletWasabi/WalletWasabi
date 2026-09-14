using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.Tests.UnitTests.WabiSabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinClientTests
{
	[Fact]
	public async Task ClientRefusesToSignWhenActualInputCountBelowConfiguredMinimum()
	{
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig()) with
		{
			MiningFeeRate = new FeeRate(1m),
			MinInputCountByRound = 21,
			TransactionSigningTimeout = TimeSpan.FromSeconds(11)
		};
		var round = WabiSabiFactory.CreateRound(roundParameters);

		// Create victim's coin and coordinator's coin
		var (keyChain, victimCoin, _) = WabiSabiFactory.CreateCoinKeyPairs();
		var (coordinatorKeyChain, coordinatorCoin, _) = WabiSabiFactory.CreateCoinKeyPairs();

		var commitment = new CoinJoinInputCommitmentData(roundParameters.CoordinationIdentifier, round.Id);
		var victimProof = keyChain.GetOwnershipProof(victimCoin, commitment);
		var coordinatorProof = coordinatorKeyChain.GetOwnershipProof(coordinatorCoin, commitment);

		// Create outputs for both parties
		using var victimOutputKey = new Key();
		using var coordinatorOutputKey = new Key();
		var victimOutput = new TxOut(
			Money.Satoshis(victimCoin.Amount.Satoshi - 105),
			victimOutputKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
		var coordinatorOutput = new TxOut(
			Money.Satoshis(coordinatorCoin.Amount.Satoshi - 105),
			coordinatorOutputKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));

		// Build the signing state with only 2 inputs (victim + coordinator)
		var signingState = new ConstructionState(roundParameters)
			.AddInput(victimCoin.Coin, victimProof, commitment)
			.AddInput(coordinatorCoin.Coin, coordinatorProof, commitment)
			.AddOutput(victimOutput)
			.AddOutput(coordinatorOutput)
			.Finalize();
		round.CoinjoinState = signingState;
		round.SetPhase(Phase.TransactionSigning);
		var roundState = RoundState.FromRound(round);

		// Set up the request handler and round state provider
		var requestHandler = new SigningCaptureRequestHandler(roundState);
		using var updaterCts = new CancellationTokenSource();
		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(requestHandler, updaterCts.Token);
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		// Create CoinJoinClient with AbsoluteMinInputCount = 21
		var coinJoinClient = new CoinJoinClient(
			_ => requestHandler,
			keyChain,
			outputProvider: null!,
			roundStateProvider,
			new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: int.MaxValue, semiPrivateThreshold: 0),
			new CoinJoinConfiguration(roundParameters.CoordinationIdentifier, 150m, AbsoluteMinInputCount: 21, AllowSoloCoinjoining: false),
			InputVerifiers.NoVerification(),
			new LiquidityClueProvider());

		// Create an AliceClient for the victim
		var aliceClient = CreateAliceClient(roundState, victimCoin, requestHandler);

		// Act: Invoke the ProceedWithSigningStateAsync method
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var signingTask = coinJoinClient.ProceedWithSigningStateAsync(
			round.Id, [aliceClient], [victimOutput], timeout.Token);

		// Deliver the coordinator's two-input signing state to the awaiting client
		roundStateUpdater.Update();

		var (_, aliceClientsThatSigned) = await signingTask;

		// Assert: With the fix, hasTooFewInputs = true (2 < 21), so mustSignAllInputs = false.
		// With only 1 alice and mustSignAllInputs = false, the client signs with
		// RemoveAt(random) which results in 0 signatures being sent.
		Assert.Empty(aliceClientsThatSigned);
		Assert.Equal(0, requestHandler.SignatureRequests);
		Assert.Null(requestHandler.CapturedSignature);
	}

	[Fact]
	public void SanityCheckTest()
	{
		var output1 = new TxOut(Money.Coins(1), BitcoinFactory.CreateScript());
		var output2 = new TxOut(Money.Coins(2), BitcoinFactory.CreateScript());
		var output3 = new TxOut(Money.Coins(3), BitcoinFactory.CreateScript());
		var output4 = new TxOut(Money.Coins(4), BitcoinFactory.CreateScript());

		// Exact match (one expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output1 },
			new[] { output1, output2, output3, output4 }));

		// Exact match (two expected)
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output3, output4 }));

		// Missing output
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, output2, output4 }));

		static TxOut AddSats(long sats, TxOut output) => new(output.Value + sats, output.ScriptPubKey);
		static TxOut AddOneSat(TxOut output) => AddSats(1, output);
		static TxOut SubOneSat(TxOut output) => AddSats(-1, output);

		// More money in one output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), output3, output4 }));

		// More money in all output
		Assert.True(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), AddOneSat(output3), output4 }));

		// Same scriptpubkeys, same amount of money but outputs were manipulated
		Assert.False(CoinJoinClient.SanityCheck(
			new[] { output2, output3 },
			new[] { output1, AddOneSat(output2), SubOneSat(output3), output4 }));
	}

	[Fact]
	public void GetTxOutsTest()
	{
		FeeRate feeRate = new(10m);

		var outputs = new[]
		{
			Output.FromDenomination(Money.Coins(1m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(2m), ScriptType.P2WPKH, feeRate),
			Output.FromDenomination(Money.Coins(3m), ScriptType.Taproot, feeRate),
			Output.FromDenomination(Money.Coins(4m), ScriptType.Taproot, feeRate),
		};

		var password = "satoshi";
		var km = ServiceFactory.CreateKeyManager(password, true);
		var destinationProvider = new InternalDestinationProvider(km);

		var txOuts = OutputProvider.GetTxOuts(outputs, destinationProvider);

		// All the outputs were generated.
		Assert.Equal(txOuts.Count(), outputs.Length);

		// No address reuse.
		Assert.Distinct(txOuts.Select(x => x.ScriptPubKey));

		// Verify if all the outputs are generated with correct ScriptType and Value.
		List<TxOut> toCheck = txOuts.ToList();
		foreach (var output in outputs)
		{
			var foundTxOut = toCheck.First(txout => txout.ScriptPubKey.IsScriptType(output.ScriptType) && txout.Value == output.Amount);
			toCheck.Remove(foundTxOut);
		}

		Assert.Empty(toCheck);
	}

	private static AliceClient CreateAliceClient(
		RoundState roundState,
		WalletWasabi.Blockchain.TransactionOutputs.SmartCoin coin,
		IWabiSabiApiRequestHandler requestHandler)
	{
		var arenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(InsecureRandom.Instance),
			roundState.CreateVsizeCredentialClient(InsecureRandom.Instance),
			roundState.CoinjoinState.Parameters.CoordinationIdentifier,
			requestHandler);

		var constructor = typeof(AliceClient)
			.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
			.Single();

		// Get the Credential type from the constructor parameters and create empty arrays
		var parameters = constructor.GetParameters();
		var credentialType = parameters[4].ParameterType.GetGenericArguments()[0]; // IEnumerable<Credential>
		var emptyCredentials = Array.CreateInstance(credentialType, 0);

		return (AliceClient)constructor.Invoke(
			[Guid.NewGuid(), roundState, arenaClient, coin, emptyCredentials, emptyCredentials]);
	}

	private sealed class SigningCaptureRequestHandler(RoundState roundState) : IWabiSabiApiRequestHandler
	{
		public int SignatureRequests { get; private set; }
		public TransactionSignaturesRequest? CapturedSignature { get; private set; }

		public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken) =>
			Task.FromResult(new RoundStateResponse([roundState]));

		public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
		{
			SignatureRequests++;
			CapturedSignature = request;
			return Task.CompletedTask;
		}

		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken) =>
			throw new NotSupportedException();
	}
}
