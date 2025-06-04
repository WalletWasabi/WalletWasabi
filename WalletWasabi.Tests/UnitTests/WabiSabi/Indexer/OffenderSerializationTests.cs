using System.Linq;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Indexer;

public class OffenderSerializationTests
{
	[Fact]
	public void SerializationTest()
	{
		var outpoint = BitcoinFactory.CreateOutPoint();
		var now = DateTimeOffset.UtcNow;
		var roundId = BitcoinFactory.CreateUint256();

		// Cheating
		var offender0 = new Offender(outpoint, now, new Cheating(roundId));
		var offender0str = offender0.ToStringLine();
		Assert.Equal(offender0str, Offender.FromStringLine(offender0str).ToStringLine());

		// Fail to confirm
		var offender1 = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotConfirm));
		var offender1str = offender1.ToStringLine();
		Assert.Equal(offender1str, Offender.FromStringLine(offender1str).ToStringLine());

		// Fail to sign
		var offender2 = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotSign));
		var offender2str = offender2.ToStringLine();
		Assert.Equal(offender2str, Offender.FromStringLine(offender2str).ToStringLine());

		// Double spent
		var offender3 = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DoubleSpent));
		var offender3str = offender3.ToStringLine();
		Assert.Equal(offender3str, Offender.FromStringLine(offender3str).ToStringLine());

		// Double spent multiple rounds
		var offender3x = new Offender(outpoint, now, new RoundDisruption(new[] { roundId, uint256.One }, Money.Satoshis(12345678), RoundDisruptionMethod.DoubleSpent));
		var offender3xstr = offender3x.ToStringLine();
		Assert.Equal(offender3xstr, Offender.FromStringLine(offender3xstr).ToStringLine());

		// Fail to verify
		var offender4 = new Offender(outpoint, now, new FailedToVerify(roundId));
		var offender4str = offender4.ToStringLine();
		Assert.Equal(offender4str, Offender.FromStringLine(offender4str).ToStringLine());

		// Fail to verify
		var ancestors = Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateOutPoint()).ToArray();
		var offender5 = new Offender(outpoint, now, new Inherited(ancestors));
		var offender5str = offender5.ToStringLine();
		Assert.Equal(offender5str, Offender.FromStringLine(offender5str).ToStringLine());

		// Fail to signal ready to sign
		var offender6 = new Offender(outpoint, now, new RoundDisruption(roundId, Money.Satoshis(12345678), RoundDisruptionMethod.DidNotSignalReadyToSign));
		var offender6str = offender6.ToStringLine();
		Assert.Equal(offender6str, Offender.FromStringLine(offender6str).ToStringLine());
	}
}
