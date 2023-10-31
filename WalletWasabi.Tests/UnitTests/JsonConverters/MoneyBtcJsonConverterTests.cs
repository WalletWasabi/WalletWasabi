using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="MoneyBtcJsonConverter"/> and <see cref="MoneyBtcJsonConverterNg"/> classes.
/// </summary>
public class MoneyBtcJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on NewtonSoft.JSON works the same.
	/// </summary>
	[Fact]
	public void FunctionalParity()
	{
		TestData testObject = new();
		string[] testValues = new[]
		{
			"209999999.97690001", // Constants.MaximumNumberOfBitcoins + 1 sat
			"210000000", // 21 million bitcoin
			"1.", // no digit after decimal point
			"1e6",
			"1,0", // Decimal comma
			"1,000.00", // Thousan separator comma
			"0.00000000000000000000000000000000000000000000001",
			"00000000000000000000000"
		};

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"Half":"0.50","One":"1.00","Zeros":"0.000001","Max":"20999999.9769","None":null,"NotAnnotated":null}""", json);

		Assert.True(AssertBothCanDeserialize(testValues[0]));
		Assert.True(AssertBothCanDeserialize(testValues[1]));
		Assert.True(AssertBothCanDeserialize(testValues[2]));
		Assert.True(AssertNoneCanDeserialize(testValues[3]));
		Assert.True(AssertNoneCanDeserialize(testValues[4]));
		Assert.True(AssertNoneCanDeserialize(testValues[5]));
		Assert.True(AssertBothCanDeserialize(testValues[6]));
		Assert.True(AssertBothCanDeserialize(testValues[7]));
	}

	private bool AssertBothCanDeserialize(string value)
	{
		try
		{
			var json = $$"""{"Half":"0.50","One":"{{value}}","Zeros":"0.000001","Max":"20999999.9769","None":null,"NotAnnotated":null}""";
			bool canDeserializeWithOld = TryDeserializeWithOld(json);
			bool canDeserializeWithNew = TryDeserializeWithNew(json);
			return canDeserializeWithOld && canDeserializeWithNew;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private bool AssertNoneCanDeserialize(string value)
	{
		try
		{
			var json = $$"""{"Half":"0.50","One":"{{value}}","Zeros":"0.000001","Max":"20999999.9769","None":null,"NotAnnotated":null}""";
			bool canDeserializeWithOld = TryDeserializeWithOld(json);
			bool canDeserializeWithNew = TryDeserializeWithNew(json);
			return !canDeserializeWithOld && !canDeserializeWithNew;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private bool TryDeserializeWithOld(string json)
	{
		try
		{
			var data = JsonConvertOld.DeserializeObject<TestData>(json);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private bool TryDeserializeWithNew(string json)
	{
		try
		{
			var data = JsonConvertNew.Deserialize<TestData>(json);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Asserts that object <paramref name="o"/> is serialized to the same JSON by both Newtonsoft library and STJ library.
	/// </summary>
	/// <returns>JSON representation of <paramref name="o"/>.</returns>
	private static string AssertSerializedEqually<T>(T o)
	{
		string newtonsoftJson = JsonConvertOld.SerializeObject(o);
		string stjJson = JsonConvertNew.Serialize(o);

		Assert.NotNull(newtonsoftJson);
		Assert.NotNull(stjJson);
		Assert.Equal(newtonsoftJson, stjJson);

		return stjJson;
	}

	/// <summary>
	/// Record with both STJ and Newtonsoft attributes.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Half))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Half))]
		public Money Half { get; set; } = Money.Coins(0.500m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(One))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(One))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money One { get; set; } = Money.Coins(1m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zeros))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zeros))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Zeros { get; set; } = Money.Coins(0.000001m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Max))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Max))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Max { get; set; } = Money.Coins(Constants.MaximumNumberOfBitcoins);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money? None { get; set; } = null;

		/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
		public Money? NotAnnotated { get; set; } = null;
	}
}
