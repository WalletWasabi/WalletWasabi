using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.DeveloperNews;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class NewsTests
	{
		[Fact]
		public void CanSerialize()
		{
			var item = new NewsItem(new Date(2020, 9, 3), "Wasabi V4 Hard Fork", "Fixed a Denial of Service attack vector.", new Uri("https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/"));
			var items = new[] { item };
			var serialized = JsonConvert.SerializeObject(items, Formatting.Indented);
			var expected = "[\r\n  {\r\n    \"Date\": \"2020-9-3\",\r\n    \"Title\": \"Wasabi V4 Hard Fork\",\r\n    \"Description\": \"Fixed a Denial of Service attack vector.\",\r\n    \"Link\": \"https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/\"\r\n  }\r\n]";
			Assert.Equal(expected, serialized);
		}

		[Fact]
		public void CanDeserialize()
		{
			var expected = new NewsItem(new Date(2020, 9, 3), "Wasabi V4 Hard Fork", "Fixed a Denial of Service attack vector.", new Uri("https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/"));
			var serialized = "[\r\n  {\r\n    \"Date\": \"2020-9-3\",\r\n    \"Title\": \"Wasabi V4 Hard Fork\",\r\n    \"Description\": \"Fixed a Denial of Service attack vector.\",\r\n    \"Link\": \"https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/\"\r\n  }\r\n]";
			var deserialized = JsonConvert.DeserializeObject<IEnumerable<NewsItem>>(serialized);
			var first = deserialized.First();
			Assert.Equal(expected.Date, first.Date);
			Assert.Equal(expected.Title, first.Title);
			Assert.Equal(expected.Description, first.Description);
			Assert.Equal(expected.Link.ToString(), first.Link.ToString());
		}

		[Fact]
		public void CanComputeHash()
		{
			var item = new NewsItem(new Date(2020, 9, 3), "Wasabi V4 Hard Fork", "Fixed a Denial of Service attack vector.", new Uri("https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/"));
			var item2 = new NewsItem(new Date(2021, 2, 3), "Foo", "Bar.", new Uri("https://blog.wasabiwallet.io/responsible-disclosure-v4-hard-fork/"));
			var items = new[] { item, item2 };
			var news = new News(items);
			var hash = news.Hash;
			var expected = "0E3BA6ED406F187ADB5DABD87B66ED295D4D5983C070493530CBB6FDDFB78B2D";
			Assert.Equal(expected, hash);
		}
	}
}
