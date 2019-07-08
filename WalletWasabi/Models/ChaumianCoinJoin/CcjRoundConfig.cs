using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CcjRoundConfig : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; internal set; }

		[JsonProperty(PropertyName = "Denomination")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; internal set; }

		[JsonProperty(PropertyName = "ConfirmationTarget")]
		public int? ConfirmationTarget { get; internal set; }

		[JsonProperty(PropertyName = "ConfirmationTargetReductionRate")]
		public double? ConfirmationTargetReductionRate { get; internal set; }

		[JsonProperty(PropertyName = "CoordinatorFeePercent")]
		public decimal? CoordinatorFeePercent { get; internal set; }

		[JsonProperty(PropertyName = "AnonymitySet")]
		public int? AnonymitySet { get; internal set; }

		[JsonProperty(PropertyName = "InputRegistrationTimeout")]
		public long? InputRegistrationTimeout { get; internal set; }

		[JsonProperty(PropertyName = "ConnectionConfirmationTimeout")]
		public long? ConnectionConfirmationTimeout { get; internal set; }

		[JsonProperty(PropertyName = "OutputRegistrationTimeout")]
		public long? OutputRegistrationTimeout { get; internal set; }

		[JsonProperty(PropertyName = "SigningTimeout")]
		public long? SigningTimeout { get; internal set; }

		[JsonProperty(PropertyName = "DosSeverity")]
		public int? DosSeverity { get; internal set; }

		[JsonProperty(PropertyName = "DosDurationHours")]
		public long? DosDurationHours { get; internal set; }

		[JsonProperty(PropertyName = "DosNoteBeforeBan")]
		public bool? DosNoteBeforeBan { get; internal set; }

		[JsonProperty(PropertyName = "MaximumMixingLevelCount")]
		public int? MaximumMixingLevelCount { get; internal set; }

		public CcjRoundConfig()
		{
		}

		public CcjRoundConfig(string filePath)
		{
			SetFilePath(filePath);
		}

		public CcjRoundConfig(Money denomination, int? confirmationTarget, double? confirmationTargetReductionRate, decimal? coordinatorFeePercent, int? anonymitySet, long? inputRegistrationTimeout, long? connectionConfirmationTimeout, long? outputRegistrationTimeout, long? signingTimeout, int? dosSeverity, long? dosDurationHours, bool? dosNoteBeforeBan, int? maximumMixingLevelCount)
		{
			FilePath = null;
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			ConfirmationTarget = Guard.NotNull(nameof(confirmationTarget), confirmationTarget);
			ConfirmationTargetReductionRate = Guard.NotNull(nameof(confirmationTargetReductionRate), confirmationTargetReductionRate);
			CoordinatorFeePercent = Guard.NotNull(nameof(coordinatorFeePercent), coordinatorFeePercent);
			AnonymitySet = Guard.NotNull(nameof(anonymitySet), anonymitySet);
			InputRegistrationTimeout = Guard.NotNull(nameof(inputRegistrationTimeout), inputRegistrationTimeout);
			ConnectionConfirmationTimeout = Guard.NotNull(nameof(connectionConfirmationTimeout), connectionConfirmationTimeout);
			OutputRegistrationTimeout = Guard.NotNull(nameof(outputRegistrationTimeout), outputRegistrationTimeout);
			SigningTimeout = Guard.NotNull(nameof(signingTimeout), signingTimeout);
			DosSeverity = Guard.NotNull(nameof(dosSeverity), dosSeverity);
			DosDurationHours = Guard.NotNull(nameof(dosDurationHours), dosDurationHours);
			DosNoteBeforeBan = Guard.NotNull(nameof(dosNoteBeforeBan), dosNoteBeforeBan);
			MaximumMixingLevelCount = Guard.NotNull(nameof(maximumMixingLevelCount), maximumMixingLevelCount);
		}

		/// <inheritdoc />
		public async Task ToFileAsync()
		{
			AssertFilePathSet();

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(FilePath,
			jsonString,
			Encoding.UTF8);
		}

		/// <inheritdoc />
		public async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();

			Denomination = Money.Coins(0.1m);
			ConfirmationTarget = Constants.OneDayConfirmationTarget; // 1 day
			ConfirmationTargetReductionRate = 0.7;
			CoordinatorFeePercent = 0.003m; // Coordinator fee percent is per anonymity set.
			AnonymitySet = 100;
			InputRegistrationTimeout = 604800; // One week
			ConnectionConfirmationTimeout = 60;
			OutputRegistrationTimeout = 60;
			SigningTimeout = 60;
			DosSeverity = 1;
			DosDurationHours = 730; // 1 month
			DosNoteBeforeBan = true;
			MaximumMixingLevelCount = 11;

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<CcjRoundConfig>($"{nameof(CcjRoundConfig)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<CcjRoundConfig>(jsonString);

				UpdateOrDefault(config);
			}

			await ToFileAsync();
		}

		public void UpdateOrDefault(CcjRoundConfig config)
		{
			Denomination = config.Denomination ?? Denomination;
			ConfirmationTarget = config.ConfirmationTarget ?? ConfirmationTarget;
			ConfirmationTargetReductionRate = config.ConfirmationTargetReductionRate ?? ConfirmationTargetReductionRate;
			CoordinatorFeePercent = config.CoordinatorFeePercent ?? CoordinatorFeePercent;
			AnonymitySet = config.AnonymitySet ?? AnonymitySet;
			InputRegistrationTimeout = config.InputRegistrationTimeout ?? InputRegistrationTimeout;
			ConnectionConfirmationTimeout = config.ConnectionConfirmationTimeout ?? ConnectionConfirmationTimeout;
			OutputRegistrationTimeout = config.OutputRegistrationTimeout ?? OutputRegistrationTimeout;
			SigningTimeout = config.SigningTimeout ?? SigningTimeout;
			DosSeverity = config.DosSeverity ?? DosSeverity;
			DosDurationHours = config.DosDurationHours ?? DosDurationHours;
			DosNoteBeforeBan = config.DosNoteBeforeBan ?? DosNoteBeforeBan;
			MaximumMixingLevelCount = config.MaximumMixingLevelCount ?? MaximumMixingLevelCount;
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(CcjRoundConfig)} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var newConfig = JsonConvert.DeserializeObject<JObject>(jsonString);
			var currentConfig = JObject.FromObject(this);

			return !JToken.DeepEquals(newConfig, currentConfig);
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null)
			{
				throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
			}
		}
	}
}
