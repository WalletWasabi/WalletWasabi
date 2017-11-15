using ConcurrentCollections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Store
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BlindedOutputStore
	{
		[JsonProperty(PropertyName = "BlindedOutputs")]
		public ConcurrentHashSet<string> BlindedOutputs { get; private set; }

		public BlindedOutputStore()
		{
			BlindedOutputs = new ConcurrentHashSet<string>();
		}

		public async Task ToFileAsync(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(
				filePath,
				jsonString,
				Encoding.UTF8);
		}

		public async static Task<BlindedOutputStore> CreateFromFileAsync(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));

			string jsonString = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<BlindedOutputStore>(jsonString);
		}
	}
}
