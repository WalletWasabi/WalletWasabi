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
		private bool? _lurkingWifeMode;

		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "WindowState")]
		[JsonConverter(typeof(WindowStateAfterSartJsonConverter))]
		public WindowState? WindowState { get; internal set; }

		[JsonProperty(PropertyName = "Height")]
		public double? Height { get; internal set; }

		[JsonProperty(PropertyName = "Width")]
		public double? Width { get; internal set; }

		[JsonProperty(PropertyName = "FeeTarget")]
		public int? FeeTarget { get; internal set; }

		[JsonProperty(PropertyName = "FeeDisplayFormat")]
		public int? FeeDisplayFormat { get; internal set; }

		[JsonProperty(PropertyName = "Autocopy")]
		public bool? Autocopy { get; internal set; }

		[JsonProperty(PropertyName = "LurkingWifeMode")]
		public bool? LurkingWifeMode
		{
			get => _lurkingWifeMode;
			set => this.RaiseAndSetIfChanged(ref _lurkingWifeMode, value);
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

			WindowState = Avalonia.Controls.WindowState.Maximized;
			Height = 530;
			Width = 1100;
			FeeTarget = 2;
			FeeDisplayFormat = 0;
			Autocopy = true;
			LurkingWifeMode = false;

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

			WindowState = config.WindowState ?? WindowState;
			Height = config.Height ?? Height;
			Width = config.Width ?? Width;
			FeeTarget = config.FeeTarget ?? FeeTarget;
			FeeDisplayFormat = config.FeeDisplayFormat ?? FeeDisplayFormat;
			Autocopy = config.Autocopy ?? Autocopy;
			LurkingWifeMode = config.LurkingWifeMode ?? LurkingWifeMode;
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(Config)} file did not exist at path: `{FilePath}`.");
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
