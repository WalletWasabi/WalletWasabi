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
using WalletWasabi.Logging;

namespace WalletWasabi.DeveloperNews
{
	[JsonObject(MemberSerialization.OptIn)]
	public class News : IEquatable<News>
	{
		public News(string filePath)
		{
			FilePath = filePath;
			if (File.Exists(FilePath))
			{
				var jsonString = File.ReadAllText(filePath, Encoding.UTF8);
				var items = JsonConvert.DeserializeObject<IEnumerable<NewsItem>>(jsonString);
				Items = new List<NewsItem>(items);
			}
			else
			{
				Items = new List<NewsItem>(Default.Items);
				ToFile();
			}
		}

		public List<NewsItem> Items { get; }
		public string Hash => ComputeHash();

		private string ComputeHash()
			=> ComputeHash(Items);

		public static string ComputeHash(IEnumerable<NewsItem> items)
			=> HashHelpers.GenerateSha256Hash(string.Join("", items.Select(x => x.ComputeHash())));

		public static News Default { get; } = new News(Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), nameof(DeveloperNews), "News.json"));
		public string FilePath { get; }

		public void ToFile()
		{
			var content = JsonConvert.SerializeObject(Items, Formatting.Indented);
			File.WriteAllText(FilePath, content);
		}

		public void Update(IEnumerable<NewsItem> items)
		{
			var hash = ComputeHash(items);
			if (hash != Hash)
			{
				Items.Clear();
				Items.AddRange(items);
				ToFile();
				Logger.LogInfo($"Updated {nameof(News)}.");
			}
		}

		public override bool Equals(object? obj) => Equals(obj as News);

		public bool Equals(News? other) => this == other;

		public override int GetHashCode() => Hash.GetHashCode();

		public static bool operator ==(News? x, News? y) => x?.Hash == y?.Hash;

		public static bool operator !=(News? x, News? y) => !(x == y);
	}
}
