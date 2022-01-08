using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

/// <summary>
/// Tests for <see cref="TransactionSignaturesRequest"/> class.
/// </summary>
public class TransactionSignaturesRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		uint256 roundId = BitcoinFactory.CreateUint256();

		using Key key1 = new();
		using Key key2 = new();

		// Request #1.
		TransactionSignaturesRequest request1 = new(
			RoundId: roundId,
			InputIndex: 1,
			Witness: new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())));

		// Request #2.
		TransactionSignaturesRequest request2 = new(
			RoundId: roundId,
			InputIndex: 1,
			Witness: new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())));

		Assert.Equal(request1, request2);

		// Request #3.
		TransactionSignaturesRequest request3 = new(
			RoundId: BitcoinFactory.CreateUint256(), // Intentionally changed.
			InputIndex: 1,
			Witness: new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())));

		Assert.NotEqual(request1, request3);

		// Request #4.
		TransactionSignaturesRequest request4 = new(
			RoundId: roundId,
			InputIndex: 999, // Intentionally changed.
			Witness: new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())));

		Assert.NotEqual(request1, request4);

		// Request #5.
		TransactionSignaturesRequest request5 = new(
			RoundId: roundId,
			InputIndex: 1,
			Witness: new WitScript(Op.GetPushOp(key2.PubKey.ToBytes()))); // Intentionally changed.

		Assert.NotEqual(request1, request5);
	}
}
