using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class CoinjoinIdStoreTests
{
	[Fact]
	public void CanAdd()
	{
		var cjIdStore = new CoinJoinIdStore();

		cjIdStore.TryAdd(uint256.One);

		Assert.True(cjIdStore.Contains(uint256.One));

		cjIdStore.TryAdd(uint256.One);
		cjIdStore.TryAdd(uint256.One);

		Assert.Single(cjIdStore.GetCoinJoinIds);
	}

	[Fact]
	public void CanValidate()
	{
		var listOfCoinjoinHashes = new List<string>
		{
			"9690826aab03c7b9ca15af2d79083445df1ac94e79858acc146efa9a52c73b5b",
			"a79e7544d32f9c5e0c3a6ed9bcbb29723125f38461bb7a735823eddc7dac7ad2",
			"90f1e3893a890ae314fba50c3dc870b0b5e5aab6e14f9e0fe9e56c95f20a2b36",
			"1fa685dbf8273369762a4f88ad1ce7f3fd14907130b878fbbb96e4140bf2bc96"
		};

		IEnumerable<uint256> ids = CoinJoinIdStore.GetValidCoinjoinIds(listOfCoinjoinHashes, out var validCoinjoinIds, out bool wasError);

		Assert.Equal(listOfCoinjoinHashes.Count, ids.Count());
		Assert.Equal(listOfCoinjoinHashes, validCoinjoinIds);
		Assert.False(wasError);
	}

	[Fact]
	public void CanTolerateError()
	{
		var listOfCoinjoinHashes = new List<string>
		{
			"9690826aab03c7b9ca15af2d79083445df1ac94e79858acc146efa9a52c73b5b",
			"dummy",
			"90f1e3893a890ae314fba50c3dc870b0b5e5aab6e14f9e0fe9e56c95f20a2b36",
			"1fa685dbf8273369762a4f88ad1ce7f3fd14907130b878fbbb96e4140bf2bc96"
		};

		IEnumerable<uint256> ids = CoinJoinIdStore.GetValidCoinjoinIds(listOfCoinjoinHashes, out var validCoinjoinIds, out bool wasError);

		Assert.Equal(ids.Count(), validCoinjoinIds.Count);
		Assert.Equal(ids.Count(), listOfCoinjoinHashes.Count - 1);
		Assert.True(wasError);
	}
}
