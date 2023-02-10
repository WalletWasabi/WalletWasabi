using Newtonsoft.Json;
using WalletWasabi.Affiliation;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class ReadyToSingRequestRequestCompatibilityTests
{
	private static JsonConverter[] Converters =
	{
		new ScalarJsonConverter(),
		new GroupElementJsonConverter(),
		new MoneySatoshiJsonConverter()
	};

	[Fact]
	public void MissingAffiliationFlag()
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23"}""";
		var deserializedReadyToSignRequestRequestWithoutAffilliation =
			JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters)!;

		Assert.Equal(AffiliationFlagConstants.Default, deserializedReadyToSignRequestRequestWithoutAffilliation.AffiliationFlag);
	}

	[Theory]
 	[InlineData("123456789012345678901")] // 21 characters.
 	[InlineData("müller")] // non-ASCII character.
 	[InlineData("MÜLLER")] // non-ASCII character.
 	[InlineData("?")]
 	[InlineData("$")]
	public void InvalidAffiliationFlag(string affiliationFlag)
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23","AffiliationFlag":"%af%"}"""
			.Replace("%af%", affiliationFlag);

		var deserializedReadyToSignRequestRequestWithoutAffilliation =
			JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters)!;

		Assert.Equal(AffiliationFlagConstants.Default, deserializedReadyToSignRequestRequestWithoutAffilliation.AffiliationFlag);
	}

	[Theory]
 	[InlineData("1")]
 	[InlineData("a")]
 	[InlineData("A")]
 	[InlineData("a1")]
 	[InlineData("A1")]
	public void ValidAffiliationFlag(string affiliationFlag)
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23","AffiliationFlag":"%af%"}"""
			.Replace("%af%", affiliationFlag);

		var deserializedReadyToSignRequestRequestWithoutAffilliation =
			JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters)!;

		Assert.Equal(affiliationFlag, deserializedReadyToSignRequestRequestWithoutAffilliation.AffiliationFlag);
	}
}
