using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	public abstract class ConfigBase : NotifyPropertyChangedBase, IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; } = null;

		public ConfigBase()
		{
		}

		public ConfigBase(string filePath)
		{
			SetFilePath(filePath);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath is null)
			{
				throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
			}
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{GetType().Name} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);

			var newConfigObject = Activator.CreateInstance(GetType());
			JsonConvert.PopulateObject(jsonString, newConfigObject);

			return !AreDeepEqual(newConfigObject);
		}

		/// <inheritdoc />
		public virtual async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();
			JsonConvert.PopulateObject("{}", this);

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo($"{GetType().Name} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				await LoadFileAsync();
			}

			await ToFileAsync();
		}

		/// <inheritdoc />
		public virtual async Task LoadFileAsync()
		{
			var jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);

			JsonConvert.PopulateObject(jsonString, this);

			if (TryEnsureBackwardsCompatibility(jsonString))
			{
				await ToFileAsync();
			}
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public bool AreDeepEqual(object otherConfig)
		{
			var currentConfig = JObject.FromObject(this);
			var otherConfigJson = JObject.FromObject(otherConfig);
			return JToken.DeepEquals(otherConfigJson, currentConfig);
		}

		/// <inheritdoc />
		public async Task ToFileAsync()
		{
			AssertFilePathSet();

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(FilePath, jsonString, Encoding.UTF8);
		}

		protected virtual bool TryEnsureBackwardsCompatibility(string jsonString) => true;
	}
}
