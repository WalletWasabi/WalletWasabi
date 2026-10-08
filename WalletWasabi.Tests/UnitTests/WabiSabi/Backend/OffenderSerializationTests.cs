using System.Linq;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class OffenderSerializationTests
{
	[Fact]
	public void SerializationTest()
	{
		var outpoint = BitcoinFactory.CreateOutPoint();
		var now = DateTimeOffset.UtcNow;
		var roundId = BitcoinFactory.CreateUint256();

		// Cheating
		{
			var offender = new Offender(outpoint, now, new Cheating(roundId));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Fail to confirm
		{
			var offender = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotConfirm));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Fail to sign
		{
			var offender = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotSign));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Double spent
		{
			var offender = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DoubleSpent));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Double spent multiple rounds
		{
			var offender = new Offender(outpoint, now, new RoundDisruption(new[] { roundId, uint256.One }, Money.Satoshis(12345678), RoundDisruptionMethod.DoubleSpent));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Fail to verify
		{
			var offender = new Offender(outpoint, now, new FailedToVerify(roundId));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Inherited
		{
			var ancestors = Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateOutPoint()).ToArray();
			var offender = new Offender(outpoint, now, new Inherited(ancestors));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}

		// Fail to signal ready to sign
		{
			var offender = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotSignalReadyToSign));
			var expected = Serialize(offender);
			var actual = Deserialize(expected);
			Assert.Equal(expected, actual);
		}
	}

	private static string Deserialize(string expected) => Offender.FromStringLine(expected).ToStringLine();

	private static string Serialize(Offender offender) => offender.ToStringLine();
}
