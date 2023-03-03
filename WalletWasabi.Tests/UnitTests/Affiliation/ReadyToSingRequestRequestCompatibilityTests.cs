using Newtonsoft.Json;
using WalletWasabi.Affiliation;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class ReadyToSignRequestRequestCompatibilityTests
{
	private static readonly JsonConverter[] Converters =
	{
		new ScalarJsonConverter(),
		new GroupElementJsonConverter(),
		new MoneySatoshiJsonConverter()
	};

	[Fact]
	public void MissingAffiliationId()
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23"}""";
		var request = JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters);

		Assert.NotNull(request);
		Assert.Equal(AffiliationConstants.DefaultAffiliationId, request.AffiliationId);
	}

	[Theory]
 	[InlineData("123456789012345678901")] // 21 characters.
 	[InlineData("müller")] // non-ASCII character.
 	[InlineData("MÜLLER")] // non-ASCII character.
 	[InlineData("?")]
 	[InlineData("$")]
	public void InvalidAffiliationId(string affiliationId)
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23","AffiliationId":"%af%"}"""
			.Replace("%af%", affiliationId);

		var deserializedReadyToSignRequestRequestWithoutAffilliation =
			JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters)!;

		Assert.Equal(AffiliationConstants.DefaultAffiliationId, deserializedReadyToSignRequestRequestWithoutAffilliation.AffiliationId);
	}

	[Theory]
 	[InlineData("1")]
 	[InlineData("a")]
 	[InlineData("a1")]
	[InlineData("A")]
 	[InlineData("A1")]
	public void ValidAffiliationId(string affiliationId)
	{
		var requestWithOutAffiliation = """{"RoundId":{"Size":32},"AliceId":"c9599f5a-bae2-4680-8200-ebe3ea945f23","AffiliationId":"%af%"}"""
			.Replace("%af%", affiliationId);

		var deserializedReadyToSignRequestRequestWithoutAffilliation =
			JsonConvert.DeserializeObject<ReadyToSignRequestRequest>(requestWithOutAffiliation, Converters)!;

		Assert.Equal(affiliationId, deserializedReadyToSignRequestRequestWithoutAffilliation.AffiliationId);
	}
}
