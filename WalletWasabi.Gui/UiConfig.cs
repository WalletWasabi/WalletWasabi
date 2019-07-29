using Avalonia.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UiConfig : ReactiveObject, IConfig
	{
		private bool _lurkingWifeMode;
		private bool _lockScreenActive;
		private string _lockScreenPinHash;

		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "WindowState")]
		[JsonConverter(typeof(WindowStateAfterStartJsonConverter))]
		public WindowState WindowState { get; internal set; } = Avalonia.Controls.WindowState.Maximized;

		[DefaultValue(530)]
		[JsonProperty(PropertyName = "Height", DefaultValueHandling = DefaultValueHandling.Populate)]
		public double Height { get; internal set; }

		[DefaultValue(1100)]
		[JsonProperty(PropertyName = "Width", DefaultValueHandling = DefaultValueHandling.Populate)]
		public double Width { get; internal set; }

		[DefaultValue(2)]
		[JsonProperty(PropertyName = "FeeTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FeeTarget { get; internal set; }

		[DefaultValue(0)]
		[JsonProperty(PropertyName = "FeeDisplayFormat", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FeeDisplayFormat { get; internal set; }

		[DefaultValue(true)]
		[JsonProperty(PropertyName = "Autocopy", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Autocopy { get; internal set; }

		[DefaultValue(false)]
		[JsonProperty(PropertyName = "LurkingWifeMode", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LurkingWifeMode
		{
			get => _lurkingWifeMode;
			set => this.RaiseAndSetIfChanged(ref _lurkingWifeMode, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = "LockScreenActive", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LockScreenActive
		{
			get => _lockScreenActive;
			set => this.RaiseAndSetIfChanged(ref _lockScreenActive, value);
		}

		[DefaultValue("")]
		[JsonProperty(PropertyName = "LockScreenPinHash", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string LockScreenPinHash
		{
			get => _lockScreenPinHash;
			set => this.RaiseAndSetIfChanged(ref _lockScreenPinHash, value);
		}

		public UiConfig()
		{
		}

		public UiConfig(string filePath)
		{
			SetFilePath(filePath);
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

			JsonConvert.PopulateObject("{}", this);

			if (!File.Exists(FilePath))
			{
				Logging.Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				await LoadFileAsync();
			}

			await ToFileAsync();
		}

		public async Task LoadFileAsync()
		{
			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<UiConfig>(jsonString);
			JsonConvert.PopulateObject(jsonString, this);
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(UiConfig)} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var newConfig = JsonConvert.DeserializeObject<UiConfig>(jsonString);

			return !AreDeepEqual(newConfig);
		}

		private bool AreDeepEqual(UiConfig otherConfig)
		{
			var currentConfig = JObject.FromObject(this);
			var otherConfigJson = JObject.FromObject(otherConfig);
			return JToken.DeepEquals(otherConfigJson, currentConfig);
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
