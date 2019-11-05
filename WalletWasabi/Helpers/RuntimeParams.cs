using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public class RuntimeParams : NotifyPropertyChangedBase
	{
		[JsonProperty(PropertyName = "NetworkNodeTimeout")]
		public int NetworkNodeTimeout { get; set; } = 64;

		[JsonConverter(typeof(VersionJsonConverter))]
		public Version DownloadedLegalDocsVersion
		{
			get => _downloadedLegalDocsVersion;
			set
			{
				if (RaiseAndSetIfChanged(ref _downloadedLegalDocsVersion, value))
				{
					OnPropertyChanged(nameof(IsLegalDocsAgreed));
				}
			}
		}

		[JsonConverter(typeof(VersionJsonConverter))]
		public Version AgreedLegalDocsVersion
		{
			get => _agreedLegalDocsVersion;
			set
			{
				if (RaiseAndSetIfChanged(ref _agreedLegalDocsVersion, value))
				{
					OnPropertyChanged(nameof(IsLegalDocsAgreed));
				}
			}
		}

		[JsonIgnore]
		public bool IsLegalDocsAgreed
		{
			get
			{
				if (AgreedLegalDocsVersion == new Version(0, 0, 0, 0))
				{
					// LegalDocs was never agreed.
					return false;
				}

				// Check version except the Revision.
				return DownloadedLegalDocsVersion.ToVersion(3) <= AgreedLegalDocsVersion.ToVersion(3);
			}
		}

		private static readonly Version AlreadyAgreedVersion = new Version(9999, 9999, 9999, 9999);

		#region Business logic

		private static RuntimeParams InternalInstance = null;

		public static RuntimeParams Instance
		{
			get
			{
				if (InternalInstance is null)
				{
					throw new InvalidOperationException($"Not loaded! Use {nameof(LoadAsync)}() first!");
				}

				if (string.IsNullOrEmpty(FileDir))
				{
					throw new InvalidOperationException("Directory not set!");
				}

				return InternalInstance;
			}
		}

		private AsyncLock AsyncLock { get; } = new AsyncLock();
		private static string FileDir;
		private Version _downloadedLegalDocsVersion = new Version(0, 0, 0, 0);
		private Version _agreedLegalDocsVersion = new Version(0, 0, 0);

		private static string FilePath => Path.Combine(FileDir, "RuntimeParams.json");

		private RuntimeParams()
		{
		}

		public static void SetDataDir(string dataDir)
		{
			FileDir = Path.Combine(dataDir);
		}

		public async Task SaveAsync()
		{
			try
			{
				using (await AsyncLock.LockAsync())
				{
					if (!Directory.Exists(FileDir))
					{
						Directory.CreateDirectory(FileDir);
					}

					string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
					await File.WriteAllTextAsync(FilePath,
					jsonString,
					Encoding.UTF8);
				}
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Could not save {nameof(RuntimeParams)}: {ex}.");
			}
		}

		public static async Task LoadAsync()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					// Create the default file
					var file = new RuntimeParams();
					await file.SaveAsync();
				}

				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);

				InternalInstance = JsonConvert.DeserializeObject<RuntimeParams>(jsonString);

				// Ensure that users who already Agreed the legal docs won't be bothered after updating.
				if (JsonHelpers.TryParseJToken(jsonString, out JToken token))
				{
					var addressString = token["AgreedLegalDocsVersion"]?.ToString()?.Trim() ?? null;
					if (addressString is null)
					{
						// The file is there but the string is missing so the client was installed before and legal docs was agreed.
						InternalInstance.AgreedLegalDocsVersion = AlreadyAgreedVersion;
						await InternalInstance.SaveAsync();
					}
				}

				return;
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Could not load {nameof(RuntimeParams)}: {ex}.");
			}
		}

		#endregion Business logic
	}
}
