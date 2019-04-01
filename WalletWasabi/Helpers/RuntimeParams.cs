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

		public static RuntimeParams Instance
		{
			get
			{
				if (_instance is null)
					_instance = LoadAsync().GetAwaiter().GetResult();
				return _instance;
			}
		}

		private static RuntimeParams _instance = null;
		private readonly AsyncLock _asyncLock = new AsyncLock();
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
				using (await _asyncLock.LockAsync())
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

		public static async Task<RuntimeParams> LoadAsync()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					var file = new RuntimeParams();
					await file.SaveAsync();
				}

				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				return JsonConvert.DeserializeObject<RuntimeParams>(jsonString);
			}
			catch (Exception ex)
			{
				Logger.LogInfo<RuntimeParams>($"Could not load RuntimeParams: {ex}.");
			}
			return new RuntimeParams();
		}

		#endregion
	}
}
