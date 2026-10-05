using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Mobile;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

[Collection("Wallet sessions")]
public class CancellationTests
{
	[Theory]
	[InlineData(1, ScriptPubKeyType.Segwit, 4)]
	[InlineData(1, ScriptPubKeyType.TaprootBIP86, 4)]
	[InlineData(2, ScriptPubKeyType.Segwit, 2)]
	[InlineData(2, ScriptPubKeyType.TaprootBIP86, 2)]
	[InlineData(3, ScriptPubKeyType.Segwit, 2)]
	public async Task UnsignedCancellationPaysForItsSignedSize(int inputCount, ScriptPubKeyType inputType, int parentRate)
	{
		const string password = "public cancellation fixture";
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using var session = new WalletSession(path, new MobileSettings { Network = "regtest" }, path);
		var wallet = await session.CreateAsync("Cancellation", password, new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), false);
		var funding = Network.RegTest.CreateTransaction();
		funding.Inputs.Add(new OutPoint(uint256.One, 0));
		for (var i = 0; i < inputCount; i++)
		{
			funding.Outputs.Add(Money.Coins(0.01m), BitcoinAddress.Create(session.Receive("funding", inputType), Network.RegTest));
		}
		wallet.TransactionProcessor.Process(new SmartTransaction(funding, (Height.ChainHeight)100u));
		using var recipient = new Key();
		var parent = wallet.BuildTransaction(password,
			new PaymentIntent(recipient.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ScriptPubKey, Money.Coins(inputCount * 0.01m - 0.001m), label: LabelsArray.Empty),
			FeeStrategy.CreateFromFeeRate(new FeeRate((decimal)parentRate)));
		Assert.Equal(inputCount, parent.SpentCoins.Count());
		var cancellation = wallet.CancelTransaction(parent.Transaction, tryToSign: false);
		Assert.False(cancellation.Signed);
		var psbt = cancellation.Psbt.Clone();
		var keys = wallet.KeyManager.GetSecrets(password, cancellation.SpentCoins.Select(c => c.ScriptPubKey).ToArray()).ToArray();
		try
		{
			var signer = Network.RegTest.CreateTransactionBuilder();
			signer.AddCoins(cancellation.SpentCoins.Select(c => c.Coin));
			signer.AddKeys(keys);
			signer.SignPSBT(psbt);
			psbt.Finalize();
			var signed = psbt.ExtractTransaction();
			Assert.Empty(signer.Check(signed));
			Assert.True(cancellation.Fee >= parent.Fee + new FeeRate(1m).GetFee(signed.GetVirtualSize()),
				$"Parent fee {parent.Fee.Satoshi}, cancellation fee {cancellation.Fee.Satoshi}, final vsize {signed.GetVirtualSize()}, unsigned vsize {cancellation.Transaction.Transaction.GetVirtualSize()}");
		}
		finally
		{
			foreach (var key in keys) { key.Dispose(); }
			wallet.ClearSensitiveKeys();
		}
	}
}
