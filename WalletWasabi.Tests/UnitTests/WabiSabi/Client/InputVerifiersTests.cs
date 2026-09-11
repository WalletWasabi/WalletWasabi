using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class InputVerifiersTests
{
	private static Coin CreateCoin() =>
		new(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1m), Script.Empty));

	private static GetTxOutResponse ResponseFor(Coin coin) =>
		new()
		{
			IsCoinBase = false,
			Confirmations = 100,
			TxOut = coin.TxOut,
		};

	[Fact]
	public async Task DetectsCoordinatorLieInAnyPositionAsync()
	{
		// With 100% sampling every input is checked, so a spent/missing UTXO
		// is caught regardless of where it sits in the list.
		var coins = Enumerable.Range(0, 20).Select(_ => CreateCoin()).ToArray();

		for (var lieIndex = 0; lieIndex < coins.Length; lieIndex++)
		{
			var missing = coins[lieIndex].Outpoint;
			var rpc = new MockRpcClient
			{
				OnGetTxOutAsync = (txid, n, _) =>
					txid == missing.Hash && n == (int)missing.N
						? null
						: ResponseFor(coins.First(c => c.Outpoint.Hash == txid && (int)c.Outpoint.N == n)),
			};

			var verify = InputVerifiers.CreateRpcVerifier(rpc, samplePercentage: 1.0);

			var ex = await Assert.ThrowsAsync<CoinJoinClientException>(
				() => verify(coins, CancellationToken.None));
			Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, ex.CoinjoinError);
		}
	}

	[Fact]
	public async Task ScriptPubKeyAndAmountMismatchAreDetectedAsync()
	{
		var coin = CreateCoin();
		var coins = new[] { coin };

		var scriptRpc = new MockRpcClient
		{
			OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 100,
				TxOut = new TxOut(coin.Amount, new Key().PubKey.WitHash.ScriptPubKey),
			},
		};
		var scriptEx = await Assert.ThrowsAsync<CoinJoinClientException>(
			() => InputVerifiers.CreateRpcVerifier(scriptRpc, 1.0)(coins, CancellationToken.None));
		Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, scriptEx.CoinjoinError);

		var amountRpc = new MockRpcClient
		{
			OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 100,
				TxOut = new TxOut(coin.Amount + Money.Coins(1m), coin.TxOut.ScriptPubKey),
			},
		};
		var amountEx = await Assert.ThrowsAsync<CoinJoinClientException>(
			() => InputVerifiers.CreateRpcVerifier(amountRpc, 1.0)(coins, CancellationToken.None));
		Assert.Equal(CoinjoinError.CoordinatorLiedAboutInputs, amountEx.CoinjoinError);
	}

	[Fact]
	public async Task SampleIsUniformOverAllInputsAsync()
	{
		// The sample must be an unbiased random subset drawn from the whole
		// input set: every input is eligible, each run picks exactly the
		// sample size, and over many runs the selection is roughly uniform.
		var coins = Enumerable.Range(0, 100).Select(_ => CreateCoin()).ToArray();
		var byOutpoint = coins.ToDictionary(c => c.Outpoint);

		var expectedSampleSize = (int)(coins.Length * 0.10);
		var counts = coins.ToDictionary(c => c.Outpoint, _ => 0);
		const int runs = 2000;

		for (var i = 0; i < runs; i++)
		{
			var queried = new List<OutPoint>();
			var rpc = new MockRpcClient
			{
				OnGetTxOutAsync = (txid, n, _) =>
				{
					var outpoint = new OutPoint(txid, n);
					queried.Add(outpoint);
					return ResponseFor(byOutpoint[outpoint]);
				},
			};

			await InputVerifiers.CreateRpcVerifier(rpc, samplePercentage: 0.10)(coins, CancellationToken.None);

			Assert.Equal(expectedSampleSize, queried.Count);
			Assert.Equal(expectedSampleSize, queried.Distinct().Count());
			foreach (var outpoint in queried)
			{
				counts[outpoint]++;
			}
		}

		// Every input must be reachable by the sampler (no input is excluded).
		Assert.All(counts.Values, count => Assert.True(count > 0, "Every input must be selectable by the sampler."));

		// Selection frequency must be close to uniform.
		var expectedPerCoin = (double)runs * expectedSampleSize / coins.Length;
		Assert.All(counts.Values, count =>
			Assert.InRange(count, expectedPerCoin * 0.5, expectedPerCoin * 1.5));
	}
}
