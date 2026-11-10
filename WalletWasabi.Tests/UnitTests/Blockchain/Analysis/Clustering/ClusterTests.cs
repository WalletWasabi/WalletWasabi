using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Analysis.Clustering;

/// <summary>
/// Tests for <see cref="Cluster"/> class.
/// </summary>
public class ClusterTests
{
	/// <summary>
	/// Tests the equality of two clusters with the same keys in different orders.
	/// </summary>
	[Fact]
	public void EqualsTest()
	{
		var km = ServiceFactory.CreateKeyManager();
		var a = BitcoinFactory.CreateHdPubKey(km);
		var b = BitcoinFactory.CreateHdPubKey(km);
		var c = BitcoinFactory.CreateHdPubKey(km);
		var ab = new Cluster([a, b]);
		var ba = new Cluster([b, a]);
		var abc = new Cluster([a, b, c]);

		// Compare [a, b] with [b, a]. Should be equal.
		Assert.Equal(ab, ba);
		Assert.True(ab.Equals(ba));
		Assert.True(ab == ba);
		Assert.Equal(ab.GetHashCode(), ba.GetHashCode());

		// Compare [a, b] with [a, b, c]. Should not be equal.
		Assert.NotEqual(ab, abc);
		Assert.False(ab.Equals(abc));
		Assert.False(ab == abc);
		Assert.NotEqual(ab.GetHashCode(), abc.GetHashCode());
	}
}
