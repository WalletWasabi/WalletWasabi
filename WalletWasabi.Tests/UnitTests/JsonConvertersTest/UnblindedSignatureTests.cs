using NBitcoin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.JsonConvertersTest
{
	public class UnblindedSignatureTests
	{
		private static Random Random = new Random(123456);

		[Fact]
		public void ConvertBackAndForth()
		{
			var converter = new UnblindedSignatureJsonConverter();
			var r = new Key();
			var key = new Key();
			var signer = new SchnorrBlinding.Signer(key);

			foreach (var i in Enumerable.Range(0, 100))
			{
				var requester = new SchnorrBlinding.Requester();

				var message = new byte[256];
				Random.NextBytes(message);
				var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
				var blindSignature = signer.Sign(blindedMessage, r);
				var unblindedSignature = requester.UnblindSignature(blindSignature);

				var sb = new StringBuilder();
				using var writer = new JsonTextWriter(new StringWriter(sb));
				converter.WriteJson(writer, unblindedSignature, null);

				using var reader = new JsonTextReader(new StringReader(sb.ToString()));
				var convertedUnblindedSignature = (UnblindedSignature)converter.ReadJson(reader, null, null, null);
				Assert.Equal(unblindedSignature.C, convertedUnblindedSignature.C);
				Assert.Equal(unblindedSignature.S, convertedUnblindedSignature.S);
			}
		}

		[Fact]
		public void DetectInvalidSerializedMessage()
		{
			var json = "[ '999999999999999999999999999999999999999999999999999999999999999999999999999999'," + // 33 bytes (INVALID)
						" '999999999999999999999999999']";

			using var reader = new JsonTextReader(new StringReader(json));
			var converter = new UnblindedSignatureJsonConverter();
			var ex = Assert.Throws<FormatException>(() => converter.ReadJson(reader, null, null, null));
			Assert.Contains("longer than 32 bytes", ex.Message);
		}
	}
}
