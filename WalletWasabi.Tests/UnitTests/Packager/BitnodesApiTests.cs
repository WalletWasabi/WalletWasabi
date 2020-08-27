using System;
using System.Collections.Generic;
using System.IO;
using WalletWasabi.Packager;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Packager
{
	/// <summary>
	/// Tests for <see cref="BitnodesApi"/>.
	/// </summary>
	public class BitnodesApiTests
	{
		private const string ApiResponse = @"
			{
			  ""timestamp"": 1597669414,
			  ""total_nodes"": 10407,
			  ""latest_height"": 644147,
			  ""nodes"": {
				""tg4uwrjmtr2jlbjy.onion:8333"": [ 70015, ""/Satoshi:0.19.0.1/"", 1597459201, 1033, 0, null, null, null, 0, 0, null, ""TOR"", ""Tor network"" ],
				""nesxfmano25clfvn.onion:8333"": [ 70015, ""/Satoshi:0.20.0/"", 1597596093, 1037, 644124, null, null, null, 0, 0, null, ""TOR"", ""Tor network"" ],
				""tep3ddikbopezl4v.onion:8333"": [ 70015, ""/Satoshi:0.19.0.1/"", 1597621378, 1032, 0, null, null, null, 0, 0, null, ""TOR"", ""Tor network"" ],
				""185.25.48.184:8333"": [ 70015, ""/Satoshi:0.19.0.1/"", 1590754138, 1037, 644147, ""4832-10449.bacloud.info"", null, ""LT"", 56, 24, ""Europe/Vilnius"", ""AS61272"", ""Informacines sistemos ir technologijos, UAB"" ]
			  }
			}";

		/// <summary>
		/// Tests that we process response from Bitnodes API correctly.
		/// </summary>
		[Fact]
		public void ProcessResponseTest()
		{
			var stringWriter = new StringWriter();
			var api = new BitnodesApi(stringWriter);
			api.ProcessResponse(ApiResponse, currentOnions: null);

			// tep3ddikbopezl4v.onion is skipped because it is not a full node.
			Assert.Equal($"nesxfmano25clfvn.onion:8333{Environment.NewLine}tg4uwrjmtr2jlbjy.onion:8333{Environment.NewLine}", stringWriter.ToString());
		}

		/// <summary>
		/// Tests reduce onions behavior where we set <c>currentOnions</c> and addresses that do not fulfill
		/// parameters (<see cref="PrintOnionsAsync(HashSet<string>? currentOnions)"/>) are filtered out.
		/// </summary>
		[Fact]
		public void ProcessResponseWithCurrentOnionsTest()
		{
			var currentOnions = new HashSet<string>()
			{
				"tg4uwrjmtr2jlbjy.onion:8333", // Fulfills all criteria.
				"tep3ddikbopezl4v.onion:8333", // This one is not a full node, so it should not be in the result.
				"185.25.48.184:8333", // Not an onion address.
			};

			var stringWriter = new StringWriter();
			var api = new BitnodesApi(stringWriter);
			api.ProcessResponse(ApiResponse, currentOnions);

			// tep3ddikbopezl4v.onion is skipped because it is not a full node.
			Assert.Equal($"tg4uwrjmtr2jlbjy.onion:8333{Environment.NewLine}", stringWriter.ToString());
		}
	}
}