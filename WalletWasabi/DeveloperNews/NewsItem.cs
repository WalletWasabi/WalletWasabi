using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto;

namespace WalletWasabi.DeveloperNews
{
	public class NewsItem
	{
		[JsonConstructor]
		public NewsItem(Date date, string title, string description, Uri link)
		{
			Date = date;
			Title = title;
			Description = description;
			Link = link;
		}

		[JsonProperty(PropertyName = "Date")]
		[JsonConverter(typeof(DateJsonConverter))]
		public Date Date { get; }

		[JsonProperty(PropertyName = "Title")]
		public string Title { get; }

		[JsonProperty(PropertyName = "Description")]
		public string Description { get; }

		[JsonProperty(PropertyName = "Link")]
		public Uri Link { get; }

		[JsonIgnore]
		public string LinkString => Link.ToString();

		public string ComputeHash()
			=> HashHelpers.GenerateSha256Hash($"{Date}{Description}{Link}{Title}");
	}
}
