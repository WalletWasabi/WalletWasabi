using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;

namespace WalletWasabi.DeveloperNews
{
	[JsonObject(MemberSerialization.OptIn)]
	public class News : IEquatable<News>
	{
		public News(IEnumerable<NewsItem> items)
		{
			Items = items;
			Hash = ComputeHash();
		}

		public IEnumerable<NewsItem> Items { get; }
		public string Hash { get; }

		private string ComputeHash()
			=> HashHelpers.GenerateSha256Hash(string.Join("", Items.Select(x => x.ComputeHash())));

		public static News Default { get; } = FromFile(Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), nameof(DeveloperNews), "News.json"));

		public static News FromFile(string filePath)
		{
			var jsonString = File.ReadAllText(filePath, Encoding.UTF8);
			var items = JsonConvert.DeserializeObject<IEnumerable<NewsItem>>(jsonString);
			return new News(items);
		}

		public static News FromFileOrDefault(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Default.ToFile(filePath);
			}
			return FromFile(filePath);
		}

		public void ToFile(string filePath)
		{
			var content = JsonConvert.SerializeObject(Items, Formatting.Indented);
			File.WriteAllText(filePath, content);
		}

		public override bool Equals(object? obj) => Equals(obj as News);

		public bool Equals(News? other) => this == other;

		public override int GetHashCode() => Hash.GetHashCode();

		public static bool operator ==(News? x, News? y) => x?.Hash == y?.Hash;

		public static bool operator !=(News? x, News? y) => !(x == y);
	}
}
