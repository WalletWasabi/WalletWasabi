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
		var ab = new Cluster([a, b]);
		var ba = new Cluster([b, a]);

		Assert.Equal(ab, ba);
		Assert.Equal(ab.GetHashCode(), ba.GetHashCode());
		Assert.NotEqual(ab, new Cluster([a]));
	}
}
