using System.Collections.Generic;
using Xunit;
using NBitcoin;
using WalletWasabi.JsonConverters.Collections;
using Newtonsoft.Json;
using System.Linq;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

public class HashSetUint256JsonConverterTests
{
	[Fact]
	public void ValidInputTest()
	{
		var testData = new TestData()
		{
			Children = [uint256.Zero],
			Parents = [uint256.One]
		};

		var jsonString = Serialize(testData);
		Assert.NotNull(jsonString);

		string expectedJsonString = $"{{\"Children\":[\"0000000000000000000000000000000000000000000000000000000000000000\"],\"Parents\":[\"0000000000000000000000000000000000000000000000000000000000000001\"]}}";
		Assert.Equal(expectedJsonString, jsonString);

		var deserializedTestData = Deserialize(jsonString);

		Assert.NotNull(deserializedTestData);

		var expectedChildren = uint256.Zero;
		var expecctedParents = uint256.One;

		Assert.Equal(expectedChildren, deserializedTestData.Children!.First());
		Assert.Equal(expecctedParents, deserializedTestData.Parents!.First());
	}

	[Fact]
	public void NullInputTest()
	{
		var testData = new TestData()
		{
			Children = null,
			Parents = null
		};

		var jsonString = Serialize(testData);
		Assert.NotNull(jsonString);

		string expectedJsonString = $"{{\"Children\":null,\"Parents\":null}}";
		Assert.Equal(expectedJsonString, jsonString);

		var deserializedTestData = Deserialize(jsonString);

		Assert.NotNull(deserializedTestData);
		Assert.Empty(deserializedTestData.Children!);
		Assert.Empty(deserializedTestData.Parents!);
	}

	private string Serialize<T>(T o)
	{
		return JsonConvert.SerializeObject(o);
	}

	private TestData Deserialize(string jsonToken)
	{
		return JsonConvert.DeserializeObject<TestData>(jsonToken) ?? throw new JsonSerializationException();
	}

	private record TestData
	{
		[JsonConverter(typeof(HashSetUint256JsonConverter))]
		public HashSet<uint256>? Children { get; set; }

		[JsonConverter(typeof(HashSetUint256JsonConverter))]
		public HashSet<uint256>? Parents { get; set; }
	}
}
