using HiddenWallet.ChaumianTumbler.Denomination;
using HiddenWallet.Converters;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
		[JsonProperty(PropertyName = "DenominationAlgorithm")]
		[JsonConverter(typeof(StringEnumConverter))]
		public DenominationAlgorithm? DenominationAlgorithm { get; private set; }

		[JsonProperty(PropertyName = "DenominationUSD")]
		public decimal? DenominationUSD { get; private set; }

		[JsonProperty(PropertyName = "DenominationBTC")]
		[JsonConverter(typeof(MoneyBtcConverter))]
		public Money DenominationBTC { get; private set; }

		[JsonProperty(PropertyName = "InputRegistrationPhaseTimeoutInSeconds")]
		public int? InputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "InputConfirmationPhaseTimeoutInSeconds")]
		public int? InputConfirmationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "OutputRegistrationPhaseTimeoutInSeconds")]
		public int? OutputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "SigningPhaseTimeoutInSeconds")]
		public int? SigningPhaseTimeoutInSeconds { get; private set; }
		
		public Config()
		{

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

		public async Task LoadOrCreateDefaultFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			var defaultDenominationAlgorithm = Denomination.DenominationAlgorithm.FixedUSD;
			var defaultDenominationUSD = 10000;
			var defaultDenominationBTC = new Money(1m, MoneyUnit.BTC);
			var defaultInputRegistrationPhaseTimeoutInSeconds = 86400; // one day
			var defaultInputConfirmationPhaseTimeoutInSeconds = 60;
			var defaultOutputRegistrationPhaseTimeoutInSeconds = 60;
			var defaultSigningPhaseTimeoutInSeconds = 60;

			if (!File.Exists(path))
			{
				DenominationAlgorithm = defaultDenominationAlgorithm;
				DenominationUSD = defaultDenominationUSD;
				DenominationBTC = defaultDenominationBTC;
				InputRegistrationPhaseTimeoutInSeconds = defaultInputRegistrationPhaseTimeoutInSeconds;
				InputConfirmationPhaseTimeoutInSeconds = defaultInputConfirmationPhaseTimeoutInSeconds;
				OutputRegistrationPhaseTimeoutInSeconds = defaultOutputRegistrationPhaseTimeoutInSeconds;
				SigningPhaseTimeoutInSeconds = defaultSigningPhaseTimeoutInSeconds;

				Console.WriteLine($"Config file did not exist. Created at path: {path}");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8, cancel);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				DenominationAlgorithm = config.DenominationAlgorithm ?? defaultDenominationAlgorithm;
				DenominationUSD = config.DenominationUSD ?? defaultDenominationUSD;
				DenominationBTC = config.DenominationBTC ?? defaultDenominationBTC;
				InputRegistrationPhaseTimeoutInSeconds = config.InputRegistrationPhaseTimeoutInSeconds ?? defaultInputRegistrationPhaseTimeoutInSeconds;
				InputConfirmationPhaseTimeoutInSeconds = config.InputConfirmationPhaseTimeoutInSeconds ?? defaultInputConfirmationPhaseTimeoutInSeconds;
				OutputRegistrationPhaseTimeoutInSeconds = config.OutputRegistrationPhaseTimeoutInSeconds ?? defaultOutputRegistrationPhaseTimeoutInSeconds;
				SigningPhaseTimeoutInSeconds = config.SigningPhaseTimeoutInSeconds ?? defaultSigningPhaseTimeoutInSeconds;
			}

			await ToFileAsync(path, cancel);
		}
	}
}
