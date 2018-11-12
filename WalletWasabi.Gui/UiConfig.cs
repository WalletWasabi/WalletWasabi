using Avalonia.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UiConfig : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "WindowState")]
		public WindowState? WindowState { get; internal set; }

		[JsonProperty(PropertyName = "Height")]
		public double? Height { get; internal set; }

		[JsonProperty(PropertyName = "Width")]
		public double? Width { get; internal set; }

		public UiConfig()
		{
		}

		public UiConfig(string filePath)
		{
			SetFilePath(filePath);
		}

		public UiConfig(WindowState windowState, double height, double width)
		{
			WindowState = Guard.NotNull(nameof(windowState), windowState);
			Height = Guard.NotNull(nameof(height), height);
			Width = Guard.NotNull(nameof(width), width);
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
