using HiddenWallet.Converters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config
	{
		[JsonProperty(PropertyName = "InputRegistrationPhaseTimeoutInSeconds", Order = 1)]
		public int InputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "InputConfirmationPhaseTimeoutInSeconds", Order = 2)]
		public int InputConfirmationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "OutputRegistrationPhaseTimeoutInSeconds", Order = 3)]
		public int OutputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "SigningPhaseTimeoutInSeconds", Order = 4)]
		public int SigningPhaseTimeoutInSeconds { get; private set; }

		public Config()
		{

		}

		public Config(
			int inputRegistrationPhaseTimeoutInSeconds,
			int inputConfirmationPhaseTimeoutInSeconds,
			int outputRegistrationPhaseTimeoutInSeconds,
			int signingPhaseTimeoutInSeconds)
		{
			InputRegistrationPhaseTimeoutInSeconds = inputRegistrationPhaseTimeoutInSeconds;
			InputConfirmationPhaseTimeoutInSeconds = inputConfirmationPhaseTimeoutInSeconds;
			OutputRegistrationPhaseTimeoutInSeconds = outputRegistrationPhaseTimeoutInSeconds;
			SigningPhaseTimeoutInSeconds = signingPhaseTimeoutInSeconds;
		}

		public async Task ToFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(path,
			jsonString,
			Encoding.UTF8,
			cancel);
		}

		public static async Task<Config> CreateFromFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));
			if (!File.Exists(path))
			{
				throw new ArgumentException($"Config file does not exist at {path}");
			}

			string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8, cancel);
			return JsonConvert.DeserializeObject<Config>(jsonString);
		}
	}
}
