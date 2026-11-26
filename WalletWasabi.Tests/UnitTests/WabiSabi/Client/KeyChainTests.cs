using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
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
		var keyChain = new KeyChain(keyManager,"");

		var coinDestination = destinationProvider.GetNextDestinations(1, false, holder: new object()).First();
		var coin = new Coin(BitcoinFactory.CreateOutPoint(), new TxOut(Money.Coins(1.0m), coinDestination));

		var transaction = Transaction.Create(Network.Main);
		transaction.Inputs.Add(coin.Outpoint);
		var psbt = PSBT.FromTransaction(transaction, Network.Main);
		psbt.Inputs[0].WitnessUtxo = coin.TxOut;

		keyChain.Sign(psbt);
		psbt.Finalize();

		var signedTx = psbt.ExtractTransaction();
		Assert.True(signedTx.HasWitness);
	}
}
