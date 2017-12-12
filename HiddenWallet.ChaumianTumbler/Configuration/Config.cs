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
using HiddenWallet.ChaumianTumbler.Configuration;
using NBitcoin.RPC;

namespace HiddenWallet.ChaumianTumbler.Configuration
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config
	{
		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcUser")]
		public string BitcoinRpcUser { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcPassword")]
		public string BitcoinRpcPassword { get; private set; }

		[JsonProperty(PropertyName = "DenominationAlgorithm")]
		[JsonConverter(typeof(StringEnumConverter))]
		public DenominationAlgorithm? DenominationAlgorithm { get; private set; }
		
		[JsonProperty(PropertyName = "DenominationUSD")]
		public decimal? DenominationUSD { get; private set; }

		[JsonProperty(PropertyName = "DenominationBTC")]
		[JsonConverter(typeof(MoneyBtcConverter))]
		public Money DenominationBTC { get; private set; }

		[JsonProperty(PropertyName = "MinimumAnonymitySet")]
		public int? MinimumAnonymitySet { get; private set; }

		[JsonProperty(PropertyName = "MaximumAnonymitySet")]
		public int? MaximumAnonymitySet { get; private set; }

		[JsonProperty(PropertyName = "AverageInputRegistrationTimeInSeconds")]
		public int? AverageTimeToSpendInInputRegistrationInSeconds { get; private set; }

		[JsonProperty(PropertyName = "InputRegistrationPhaseTimeoutInSeconds")]
		public int? InputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "ConnectionConfirmationPhaseTimeoutInSeconds")]
		public int? ConnectionConfirmationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "OutputRegistrationPhaseTimeoutInSeconds")]
		public int? OutputRegistrationPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "SigningPhaseTimeoutInSeconds")]
		public int? SigningPhaseTimeoutInSeconds { get; private set; }

		[JsonProperty(PropertyName = "MaximumInputsPerAlices")]
		public int? MaximumInputsPerAlices { get; private set; }

		[JsonProperty(PropertyName = "FallBackSatoshiFeePerBytes")]
		public int? FallBackSatoshiFeePerBytes { get; private set; }

		[JsonProperty(PropertyName = "FeeConfirmationTarget")]
		public int? FeeConfirmationTarget { get; private set; }

		[JsonProperty(PropertyName = "FeeEstimationMode")]
		[JsonConverter(typeof(EstimateSmartFeeModeConverter))]
		public EstimateSmartFeeMode FeeEstimationMode { get; private set; }

		public Config()
		{

		}

		public async Task ToFileAsync(string path, CancellationToken cancel)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(path,
			jsonString,
			Encoding.UTF8,
			cancel);
		}

		public async Task LoadOrCreateDefaultFileAsync(string path, CancellationToken cancel)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));

			Network = Network.Main;
			BitcoinRpcUser = "user";
			BitcoinRpcPassword = "password";
			DenominationAlgorithm = Configuration.DenominationAlgorithm.FixedUSD;
			DenominationUSD = 10000;
			DenominationBTC = new Money(1m, MoneyUnit.BTC);
			MinimumAnonymitySet = 3;
			MaximumAnonymitySet = 100; // for now, in theory 300-400 should be fine, too
			AverageTimeToSpendInInputRegistrationInSeconds = 180; // 3min
			InputRegistrationPhaseTimeoutInSeconds = 86400; // one day
			ConnectionConfirmationPhaseTimeoutInSeconds = 60;
			OutputRegistrationPhaseTimeoutInSeconds = 60;
			SigningPhaseTimeoutInSeconds = 60;
			MaximumInputsPerAlices = 7;
			FallBackSatoshiFeePerBytes = 300;
			FeeConfirmationTarget = 2;
			FeeEstimationMode = EstimateSmartFeeMode.Economical;

			if (!File.Exists(path))
			{
				Console.WriteLine($"Config file did not exist. Created at path: {path}");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8, cancel);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				Network = config.Network ?? Network;
				BitcoinRpcUser = config.BitcoinRpcUser ?? BitcoinRpcUser;
				BitcoinRpcPassword = config.BitcoinRpcPassword ?? BitcoinRpcPassword;
				DenominationAlgorithm = config.DenominationAlgorithm ?? DenominationAlgorithm;
				DenominationUSD = config.DenominationUSD ?? DenominationUSD;
				DenominationBTC = config.DenominationBTC ?? DenominationBTC;
				MinimumAnonymitySet = config.MinimumAnonymitySet ?? MinimumAnonymitySet;
				MaximumAnonymitySet = config.MaximumAnonymitySet ?? MaximumAnonymitySet;
				AverageTimeToSpendInInputRegistrationInSeconds = config.AverageTimeToSpendInInputRegistrationInSeconds ?? AverageTimeToSpendInInputRegistrationInSeconds;
				InputRegistrationPhaseTimeoutInSeconds = config.InputRegistrationPhaseTimeoutInSeconds ?? InputRegistrationPhaseTimeoutInSeconds;
				ConnectionConfirmationPhaseTimeoutInSeconds = config.ConnectionConfirmationPhaseTimeoutInSeconds ?? ConnectionConfirmationPhaseTimeoutInSeconds;
				OutputRegistrationPhaseTimeoutInSeconds = config.OutputRegistrationPhaseTimeoutInSeconds ?? OutputRegistrationPhaseTimeoutInSeconds;
				SigningPhaseTimeoutInSeconds = config.SigningPhaseTimeoutInSeconds ?? SigningPhaseTimeoutInSeconds;
				MaximumInputsPerAlices = config.MaximumInputsPerAlices ?? MaximumInputsPerAlices;
				FallBackSatoshiFeePerBytes = config.FallBackSatoshiFeePerBytes ?? FallBackSatoshiFeePerBytes;
				FeeConfirmationTarget = config.FeeConfirmationTarget ?? FeeConfirmationTarget;
				FeeEstimationMode = config.FeeEstimationMode;
			}

			await ToFileAsync(path, cancel);
		}
	}
}
