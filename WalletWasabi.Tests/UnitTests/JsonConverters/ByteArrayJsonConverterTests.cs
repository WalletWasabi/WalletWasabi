using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.JsonConverters
{
	/// <summary>
	/// Tests for <see cref="ByteArrayJsonConverter"/> class.
	/// </summary>
	public class ByteArrayJsonConverterTests
	{
		[Fact]
		public void RoundtripTest()
		{
			BinaryConverter converter = new();
			byte[] inputArray = { 0x00, 0x01, 0x02 };

			string json = JsonConvert.SerializeObject(inputArray, converter);
			Assert.Equal("\"AAEC\"", json);

			byte[]? actualArray = JsonConvert.DeserializeObject<byte[]>(json, converter);
			Assert.NotNull(actualArray);
			Assert.Equal(inputArray, actualArray);
		}

		[Fact]
		public void NullTest()
		{
			BinaryConverter converter = new();

			byte[]? actualArray = JsonConvert.DeserializeObject<byte[]>("null", converter);
			Assert.Null(actualArray);
		}

		[Fact]
		public void InvalidInputTest()
		{
			BinaryConverter converter = new();

			_ = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<byte[]>("invalid", converter));
			_ = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<byte[]>("NULL", converter)); // Only "null" is allowed by JSON grammar.
			_ = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<byte[]>("{}", converter));
			_ = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<byte[]>("{", converter));
			_ = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<byte[]>("}", converter));
		}
	}
}
