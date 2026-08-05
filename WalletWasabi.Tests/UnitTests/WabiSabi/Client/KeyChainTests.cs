using NBitcoin;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class KeyChainTests
{
	[Fact]
	public void SignTransactionTest()
	{
		var keyManager = KeyManager.CreateNew(out _, "", Network.Main);
		var destinationProvider = new InternalDestinationProvider(keyManager);
		var keyChain = new KeyChain(keyManager,"");

		var coinDestination = destinationProvider.GetNextDestinations(1, false).First();
		var coin = new Coin(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1.0m), coinDestination));

		var transaction = Transaction.Create(Network.Main); // the transaction doesn't contain the input that we request to be signed.

		Assert.Throws<ArgumentException>(() => keyChain.Sign(WithPrecomputedData(transaction), coin));

		transaction.Inputs.Add(coin.Outpoint);
		var signedTx = keyChain.Sign(WithPrecomputedData(transaction, coin), coin);
		Assert.True(signedTx.HasWitness);
	}

	private static TransactionWithPrecomputedData WithPrecomputedData(Transaction transaction, params Coin[] coins) =>
		new(transaction, transaction.PrecomputeTransactionData(coins), ImmutableDictionary<OutPoint, WalletWasabi.Crypto.OwnershipProof>.Empty);
}
