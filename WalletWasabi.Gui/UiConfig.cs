using Avalonia.Controls;
using Newtonsoft.Json;
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
		public bool LurkingWifeMode
		{

				get => _lurkingWifeMode ?? false;
				set => this.RaiseAndSetIfChanged(ref _lurkingWifeMode, value);
		}

		public UiConfig()
		{
		}

		public UiConfig(string filePath)
		{
			SetFilePath(filePath);
		}

		public UiConfig(WindowState windowState, double height, double width, int feeTarget, int feeDisplayFormat, bool autocopy, bool lurkingWifeMode)
		{
			WindowState = Guard.NotNull(nameof(windowState), windowState);
			Height = Guard.NotNull(nameof(height), height);
			Width = Guard.NotNull(nameof(width), width);
			FeeTarget = Guard.NotNull(nameof(feeTarget), feeTarget);
			FeeDisplayFormat = Guard.NotNull(nameof(feeDisplayFormat), feeDisplayFormat);
			Autocopy = Guard.NotNull(nameof(autocopy), autocopy);
			LurkingWifeMode = Guard.NotNull(nameof(lurkingWifeMode), lurkingWifeMode);
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
			LurkingWifeMode = config.LurkingWifeMode;
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
			var config = JsonConvert.DeserializeObject<UiConfig>(jsonString);

			if (WindowState != config.WindowState)
			{
				return true;
			}

			if (Height != config.Height)
			{
				return true;
			}

			if (Width != config.Width)
			{
				return true;
			}

			if (FeeTarget != config.FeeTarget)
			{
				return true;
			}

			if (FeeDisplayFormat != config.FeeDisplayFormat)
			{
				return true;
			}

			if (Autocopy != config.Autocopy)
			{
				return true;
			}

			if (LurkingWifeMode != config.LurkingWifeMode)
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
			if (FilePath is null)
			{
				throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
			}
		}
	}
}
