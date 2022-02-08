using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
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
		var keyChain = new KeyChain(keyManager, new Kitchen(""));

		var coinDestination = destinationProvider.GetNextDestinations(1).First();
		var coin = new Coin(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1.0m), coinDestination));
		var ownershipProof = keyChain.GetOwnershipProof(coinDestination, new CoinJoinInputCommitmentData("test", uint256.One));

		var transaction = Transaction.Create(Network.Main); // the transaction doesn't contain the input that we request to be signed.

		Assert.Throws<ArgumentException>(() => keyChain.Sign(transaction, coin, ownershipProof));

		transaction.Inputs.Add(coin.Outpoint);
		var signedTx = keyChain.Sign(transaction, coin, ownershipProof);
		Assert.True(signedTx.HasWitness);
	}
}
