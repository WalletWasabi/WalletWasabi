using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public class RuntimeParams
	{
		[JsonProperty(PropertyName = "NetworkNodeTimeout")]
		public int NetworkNodeTimeout { get; set; } = 64;

		#region Business logic

		private static RuntimeParams InternalInstance = null;

		public static RuntimeParams Instance
		{
			get
			{
				if (InternalInstance is null)
				{
					throw new InvalidOperationException("Not loaded! Use LoadAsync() first!");
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
				InternalInstance = JsonConvert.DeserializeObject<RuntimeParams>(jsonString);
				return;
			}
			catch (Exception ex)
			{
				Logger.LogInfo<RuntimeParams>($"Could not load RuntimeParams: {ex}.");
			}
			InternalInstance = new RuntimeParams();
		}

		#endregion Business logic
	}
}
