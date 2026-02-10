using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Exceptions;
using WalletWasabi.Tests.UnitTests;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class UtxoVerificationTests
{
	private static MethodInfo GetVerifyUtxosMethod()
	{
		return typeof(CoinJoinClient).GetMethod("VerifyUtxosAsync", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("VerifyUtxosAsync method not found.");
	}

	private static RoundState CreateTestRoundState()
	{
		var round = WabiSabiFactory.CreateRound(new WabiSabiConfig());
		return RoundState.FromRound(round);
	}

	[Fact]
	public async Task NullRpcClient_SkipsVerification()
	{
		var client = CreateCoinJoinClient(bitcoinRpcClient: null);
		var method = GetVerifyUtxosMethod();

		var coin = CreateCoin(Money.Coins(1m));

		await (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!;
	}

	[Fact]
	public async Task ValidUtxos_PassVerification()
	{
		var coin = CreateCoin(Money.Coins(1m));
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) =>
			new GetTxOutResponse
			{
				TxOut = coin.TxOut,
				Confirmations = 6
			};

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		await (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!;
	}

	[Fact]
	public async Task NonExistentUtxo_ThrowsCoordinatorLied()
	{
		var coin = CreateCoin(Money.Coins(1m));
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) => null;

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		var ex = await AssertCoinJoinClientExceptionAsync(
			() => (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!);

		Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, ex.CoinjoinError);
		Assert.Contains("does not exist", ex.Message);
	}

	[Fact]
	public async Task AmountMismatch_ThrowsCoordinatorLied()
	{
		var coin = CreateCoin(Money.Coins(1m));
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) =>
			new GetTxOutResponse
			{
				TxOut = new TxOut(Money.Coins(0.5m), coin.TxOut.ScriptPubKey),
				Confirmations = 6
			};

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		var ex = await AssertCoinJoinClientExceptionAsync(
			() => (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!);

		Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, ex.CoinjoinError);
		Assert.Contains("amount mismatch", ex.Message);
	}

	[Fact]
	public async Task ScriptMismatch_ThrowsCoordinatorLied()
	{
		var coin = CreateCoin(Money.Coins(1m));
		var differentScript = new Key().GetScriptPubKey(ScriptPubKeyType.Segwit);
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) =>
			new GetTxOutResponse
			{
				TxOut = new TxOut(coin.TxOut.Value, differentScript),
				Confirmations = 6
			};

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		var ex = await AssertCoinJoinClientExceptionAsync(
			() => (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!);

		Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, ex.CoinjoinError);
		Assert.Contains("scriptPubKey mismatch", ex.Message);
	}

	[Fact]
	public async Task RpcFailure_ProceedsWithWarning()
	{
		var coin = CreateCoin(Money.Coins(1m));
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) =>
			throw new HttpRequestException("Connection refused");

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		// RPC failure should be handled gracefully, not thrown.
		await (Task)method.Invoke(client, [new[] { coin }, CreateTestRoundState(), CancellationToken.None])!;
	}

	/// <summary>
	/// Reflection-based method.Invoke wraps exceptions in TargetInvocationException.
	/// This helper unwraps them to get the actual CoinJoinClientException.
	/// </summary>
	private static async Task<CoinJoinClientException> AssertCoinJoinClientExceptionAsync(Func<Task> action)
	{
		try
		{
			await action();
			throw new Xunit.Sdk.XunitException("Expected CoinJoinClientException but no exception was thrown.");
		}
		catch (TargetInvocationException tie) when (tie.InnerException is CoinJoinClientException cjce)
		{
			return cjce;
		}
		catch (CoinJoinClientException cjce)
		{
			return cjce;
		}
	}

	[Fact]
	public async Task EmptyCoins_SkipsVerification()
	{
		var mockRpc = new MockRpcClient();
		var callCount = 0;
		mockRpc.OnGetTxOutAsync = (txId, index, includeMempool) =>
		{
			callCount++;
			return new GetTxOutResponse { TxOut = new TxOut(), Confirmations = 1 };
		};

		var client = CreateCoinJoinClient(bitcoinRpcClient: mockRpc);
		var method = GetVerifyUtxosMethod();

		await (Task)method.Invoke(client, [Array.Empty<Coin>(), CreateTestRoundState(), CancellationToken.None])!;
		Assert.Equal(0, callCount);
	}

	private static Coin CreateCoin(Money amount)
	{
		var key = new Key();
		var scriptPubKey = key.GetScriptPubKey(ScriptPubKeyType.Segwit);
		var txid = RandomUtils.GetUInt256();
		var outpoint = new OutPoint(txid, 0);
		return new Coin(outpoint, new TxOut(amount, scriptPubKey));
	}

	private static CoinJoinClient CreateCoinJoinClient(IRPCClient? bitcoinRpcClient)
	{
		return new CoinJoinClient(
			arenaRequestHandlerFactory: _ => throw new NotImplementedException(),
			keyChain: null!,
			outputProvider: null!,
			roundStatusProvider: null!,
			coinJoinCoinSelector: null!,
			coinJoinConfiguration: new WalletWasabi.WabiSabi.Client.CoinJoinConfiguration("CoinJoinCoordinatorIdentifier", 350m, 2, false),
			liquidityClueProvider: new LiquidityClueProvider(),
			bitcoinRpcClient: bitcoinRpcClient);
	}
}
