using Newtonsoft.Json;
using System;
using WalletWasabi.CoinJoin.Common.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class InputsResponseTests
	{
		[Fact]
		public void InputsResponseSerialization()
		{
			var resp = new InputsResponse
			{
				UniqueId = Guid.NewGuid(),
				RoundId = 1,
			};
			var serialized = JsonConvert.SerializeObject(resp);
			var deserialized = JsonConvert.DeserializeObject<InputsResponse>(serialized);

			Assert.Equal(resp.RoundId, deserialized.RoundId);
			Assert.Equal(resp.UniqueId, deserialized.UniqueId);
		}
	}
}
