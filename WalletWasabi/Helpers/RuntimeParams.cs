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
using WalletWasabi.Services;

namespace WalletWasabi.Helpers
{
	public class RuntimeParams : NotifyPropertyChangedBase
	{
		[JsonProperty(PropertyName = "NetworkNodeTimeout")]
		public int NetworkNodeTimeout { get; set; } = 64;

		[JsonConverter(typeof(VersionJsonConverter))]
		public Version DownloadedLegalDocsVersion { get; set; } = new Version(0, 0, 0, 0);

		[JsonConverter(typeof(VersionJsonConverter))]
		public Version AgreedLegalDocsVersion { get; set; } = new Version(0, 0, 0, 0);

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

				await LegalDocsManager.EnsureCompatiblityAsync(jsonString);

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
