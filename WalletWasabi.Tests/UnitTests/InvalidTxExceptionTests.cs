using System.Linq;
using NBitcoin;
using NBitcoin.Policy;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Exceptions;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class InvalidTxExceptionTests
{
	[Fact]
	public void ExceptionMessageContainsUsefulInformation()
	{
		var crazyInvalidTx = BitcoinFactory.CreateSmartTransaction(
			9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 1) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		TransactionPolicyError[] crazyInvalidTxErrors =
			{
					new NotEnoughFundsPolicyError("Fees different than expected"),
					new OutputPolicyError("Output value should not be less than zero", -10),
				};

		var ex = new InvalidTxException(crazyInvalidTx.Transaction, crazyInvalidTxErrors);
		Assert.Contains(crazyInvalidTxErrors[0].ToString(), ex.Message);
		Assert.Contains(crazyInvalidTxErrors[1].ToString(), ex.Message);
	}
}
