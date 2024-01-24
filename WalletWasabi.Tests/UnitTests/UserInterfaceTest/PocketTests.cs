using System.Linq;
using WalletWasabi.Fluent.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class PocketTests
{
	[Fact]
	public void MergeTests()
	{
		var emptyPocketArray = Array.Empty<Pocket>();
		var emptyPocket = Pocket.Empty;
		var pocket1 = LabelTestExtensions.CreateSingleCoinPocket(0.5m, "Pocket 1");
		var pocket2 = LabelTestExtensions.CreateSingleCoinPocket(0.2m, "Pocket 2");
		var pocket3 = LabelTestExtensions.CreateSingleCoinPocket(0.4m, "Pocket 3");
		var pocketArray = new[] { pocket1, pocket2 };

		// Two not empty pocket
		var mergedPocket = Pocket.Merge(pocket1, pocket2);
		Assert.True(pocket1.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.True(pocket2.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.Equal(pocket1.Amount + pocket2.Amount, mergedPocket.Amount);

		// Empty with non empty pocket
		mergedPocket = Pocket.Merge(pocket1, emptyPocket);
		Assert.Equal(pocket1.Labels, mergedPocket.Labels);
		Assert.Equal(pocket1.Amount, mergedPocket.Amount);
		Assert.Equal(pocket1.Coins, mergedPocket.Coins);

		// Array merged together
		mergedPocket = Pocket.Merge(pocketArray);
		Assert.True(pocket1.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.True(pocket2.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.Equal(pocket1.Amount + pocket2.Amount, mergedPocket.Amount);

		// Empty array merged together
		mergedPocket = Pocket.Merge(emptyPocketArray);
		Assert.Equal(emptyPocket.Labels, mergedPocket.Labels);
		Assert.Equal(emptyPocket.Amount, mergedPocket.Amount);
		Assert.Equal(emptyPocket.Coins, mergedPocket.Coins);

		// Non empty array with pocket
		mergedPocket = Pocket.Merge(pocketArray, pocket3);
		Assert.True(pocket1.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.True(pocket2.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.True(pocket3.Labels.All(label => mergedPocket.Labels.Contains(label)));
		Assert.Equal(pocket1.Amount + pocket2.Amount + pocket3.Amount, mergedPocket.Amount);

		// Empty array with a pocket merged together
		mergedPocket = Pocket.Merge(emptyPocketArray, pocket1);
		Assert.Equal(pocket1.Labels, mergedPocket.Labels);
		Assert.Equal(pocket1.Amount, mergedPocket.Amount);
		Assert.Equal(pocket1.Coins, mergedPocket.Coins);

		// Empty array with empty pocket
		mergedPocket = Pocket.Merge(emptyPocketArray, emptyPocket);
		Assert.Equal(emptyPocket.Labels, mergedPocket.Labels);
		Assert.Equal(emptyPocket.Amount, mergedPocket.Amount);
		Assert.Equal(emptyPocket.Coins, mergedPocket.Coins);

		// Ensure uniqueness
		mergedPocket = Pocket.Merge(pocket1, pocket1, pocket1, pocket1);
		Assert.Equal(pocket1.Amount, mergedPocket.Amount);
		Assert.Equal(pocket1.Coins, mergedPocket.Coins);
		Assert.Equal(pocket1.Labels, mergedPocket.Labels);
	}
}
