using Newtonsoft.Json;
using System.Collections.Generic;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class StrobeTestSet
{
	[JsonConstructor]
	public StrobeTestSet(List<StrobeTestVector> testVectors)
	{
		TestVectors = testVectors;
	}

	[JsonProperty(PropertyName = "test_vectors")]
	public List<StrobeTestVector> TestVectors { get; }
}
