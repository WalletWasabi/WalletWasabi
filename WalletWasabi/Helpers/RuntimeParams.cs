using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nito.AsyncEx;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public class RuntimeParams
	{
		[JsonProperty(PropertyName = "NetworkNodeTimeout")]
		public int NetworkNodeTimeout { get; set; } = 64;

		#region Business logic

		private static RuntimeParams _instance = null;

		public static RuntimeParams Instance
		{
			get
			{
				if (_instance is null)
					throw new InvalidOperationException("Not loaded! Use LoadAsync() first!");
				return _instance;
			}
		}

		private AsyncLock AsyncLock { get; } = new AsyncLock();
		private static readonly string FileDir = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Data");
		private static readonly string FilePath = Path.Combine(FileDir, "RuntimeParams.json");

		// Explicit static constructor to tell C# compiler, not to mark type as beforefieldinit.
		static RuntimeParams()
		{
		}

		private RuntimeParams()
		{
		}

		public async Task SaveAsync()
		{
			try
			{
				using (await AsyncLock.LockAsync())
				{
					if (!Directory.Exists(FileDir))
						Directory.CreateDirectory(FileDir);
					string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
					await File.WriteAllTextAsync(FilePath,
					jsonString,
					Encoding.UTF8);
				}
			}
			catch (Exception ex)
			{
				Logger.LogInfo<RuntimeParams>($"Could not save RuntimeParams: {ex}.");
			}
		}

		public static async Task LoadAsync()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					var file = new RuntimeParams();
					await file.SaveAsync();
				}

				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				_instance = JsonConvert.DeserializeObject<RuntimeParams>(jsonString);
				return;
			}
			catch (Exception ex)
			{
				Logger.LogInfo<RuntimeParams>($"Could not load RuntimeParams: {ex}.");
			}
			_instance = new RuntimeParams();
		}

		#endregion Business logic
	}
}
