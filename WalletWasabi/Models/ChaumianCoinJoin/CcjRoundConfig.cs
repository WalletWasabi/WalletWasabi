using NBitcoin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CcjRoundConfig : IConfig
	{
		private Money _denomination;

		/// <inheritdoc />
		public string FilePath { get; internal set; }

		public Money CurrentDenomination { get; internal set; }

		[JsonProperty(PropertyName = "Denomination")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		private Money Denomination
		{
			get => _denomination;
			set
			{
				if (value != _denomination)
				{
					_denomination = value;
					CurrentDenomination = value;
				}
			}
		}

		internal void SetDenomination(Money denomination)
		{
			Denomination = denomination;
		}

		[JsonProperty(PropertyName = "ConfirmationTarget")]
		public int? ConfirmationTarget { get; internal set; }

		[JsonProperty(PropertyName = "CoordinatorFeePercent")]
		public decimal? CoordinatorFeePercent { get; internal set; }

		[JsonProperty(PropertyName = "AnonymitySet")]
		public int? AnonymitySet { get; set; }

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
		[JsonProperty(PropertyName = "ExpectedRoundsPerDay")]
		public int? ExpectedRoundsPerDay { get; internal set; }

		public CcjRoundConfig()
		{
		}

		public CcjRoundConfig(string filePath)
		{
			SetFilePath(filePath);
		}

		public CcjRoundConfig(Money denomination, int? confirmationTarget, decimal? coordinatorFeePercent, int? anonymitySet, long? inputRegistrationTimeout, long? connectionConfirmationTimeout, long? outputRegistrationTimeout, long? signingTimeout, int? dosSeverity, long? dosDurationHours, bool dosNoteBeforeBan, int? expectedRoundsPerDay)
		{
			FilePath = null;
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			ConfirmationTarget = Guard.NotNull(nameof(confirmationTarget), confirmationTarget);
			CoordinatorFeePercent = Guard.NotNull(nameof(coordinatorFeePercent), coordinatorFeePercent);
			AnonymitySet = Guard.NotNull(nameof(anonymitySet), anonymitySet);
			InputRegistrationTimeout = Guard.NotNull(nameof(inputRegistrationTimeout), inputRegistrationTimeout);
			ConnectionConfirmationTimeout = Guard.NotNull(nameof(connectionConfirmationTimeout), connectionConfirmationTimeout);
			OutputRegistrationTimeout = Guard.NotNull(nameof(outputRegistrationTimeout), outputRegistrationTimeout);
			SigningTimeout = Guard.NotNull(nameof(signingTimeout), signingTimeout);
			DosSeverity = Guard.NotNull(nameof(dosSeverity), dosSeverity);
			DosDurationHours = Guard.NotNull(nameof(dosDurationHours), dosDurationHours);
			DosNoteBeforeBan = Guard.NotNull(nameof(dosNoteBeforeBan), dosNoteBeforeBan);
			ExpectedRoundsPerDay = Guard.NotNull(nameof(expectedRoundsPerDay), expectedRoundsPerDay);
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
			ConfirmationTarget = 144; // 1 day
			CoordinatorFeePercent = 0.003m; // Coordinator fee percent is per anonymity set.
			AnonymitySet = 100;
			InputRegistrationTimeout = 604800; // One week
			ConnectionConfirmationTimeout = 60;
			OutputRegistrationTimeout = 60;
			SigningTimeout = 60;
			DosSeverity = 1;
			DosDurationHours = 730; // 1 month
			DosNoteBeforeBan = true;
			ExpectedRoundsPerDay = 2; 

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
			CoordinatorFeePercent = config.CoordinatorFeePercent ?? CoordinatorFeePercent;
			AnonymitySet = config.AnonymitySet ?? AnonymitySet;
			InputRegistrationTimeout = config.InputRegistrationTimeout ?? InputRegistrationTimeout;
			ConnectionConfirmationTimeout = config.ConnectionConfirmationTimeout ?? ConnectionConfirmationTimeout;
			OutputRegistrationTimeout = config.OutputRegistrationTimeout ?? OutputRegistrationTimeout;
			SigningTimeout = config.SigningTimeout ?? SigningTimeout;
			DosSeverity = config.DosSeverity ?? DosSeverity;
			DosDurationHours = config.DosDurationHours ?? DosDurationHours;
			DosNoteBeforeBan = config.DosNoteBeforeBan ?? DosNoteBeforeBan;
			ExpectedRoundsPerDay = config.ExpectedRoundsPerDay ?? ExpectedRoundsPerDay;
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
			var config = JsonConvert.DeserializeObject<CcjRoundConfig>(jsonString);

			if (Denomination != config.Denomination)
			{
				return true;
			}
			if (ConfirmationTarget != config.ConfirmationTarget)
			{
				return true;
			}
			if (CoordinatorFeePercent != config.CoordinatorFeePercent)
			{
				return true;
			}
			if (AnonymitySet != config.AnonymitySet)
			{
				return true;
			}
			if (InputRegistrationTimeout != config.InputRegistrationTimeout)
			{
				return true;
			}
			if (ConnectionConfirmationTimeout != config.ConnectionConfirmationTimeout)
			{
				return true;
			}
			if (OutputRegistrationTimeout != config.OutputRegistrationTimeout)
			{
				return true;
			}
			if (SigningTimeout != config.SigningTimeout)
			{
				return true;
			}
			if (DosSeverity != config.DosSeverity)
			{
				return true;
			}
			if (DosDurationHours != config.DosDurationHours)
			{
				return true;
			}
			if (DosNoteBeforeBan != config.DosNoteBeforeBan)
			if (ExpectedRoundsPerDay != config.ExpectedRoundsPerDay)
			{
				return true;
			}

			return false;
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null) throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}
}
