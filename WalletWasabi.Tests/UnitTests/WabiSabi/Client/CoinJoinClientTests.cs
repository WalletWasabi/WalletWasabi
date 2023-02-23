using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinClientTests
{
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
}
