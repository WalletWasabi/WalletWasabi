using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
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
			var serialized = JsonSerializer.Serialize(resp);
			var deserialized = JsonSerializer.Deserialize<InputsResponse>(serialized);

			Assert.Equal(resp.RoundId, deserialized.RoundId);
			Assert.Equal(resp.UniqueId, deserialized.UniqueId);
		}
	}
}
