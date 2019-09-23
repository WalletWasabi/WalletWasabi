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
using WalletWasabi.Bases;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UiConfig : ConfigBase
	{
		private bool _lurkingWifeMode;
		private bool _lockScreenActive;
		private string _lockScreenPinHash;
		private bool _isCustomFee;
		private bool _autocopy;

		[JsonProperty(PropertyName = "WindowState")]
		[JsonConverter(typeof(WindowStateAfterStartJsonConverter))]
		public WindowState WindowState { get; internal set; } = WindowState.Maximized;

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
		public bool Autocopy
		{
			get => _autocopy;
			set => RaiseAndSetIfChanged(ref _autocopy, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = "IsCustomFee", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsCustomFee
		{
			get => _isCustomFee;
			set => RaiseAndSetIfChanged(ref _isCustomFee, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = "LurkingWifeMode", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LurkingWifeMode
		{
			get => _lurkingWifeMode;
			set => RaiseAndSetIfChanged(ref _lurkingWifeMode, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = "LockScreenActive", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LockScreenActive
		{
			get => _lockScreenActive;
			set => RaiseAndSetIfChanged(ref _lockScreenActive, value);
		}

		[DefaultValue("")]
		[JsonProperty(PropertyName = "LockScreenPinHash", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string LockScreenPinHash
		{
			get => _lockScreenPinHash;
			set => RaiseAndSetIfChanged(ref _lockScreenPinHash, value);
		}

		public UiConfig() : base()
		{
		}

		public UiConfig(string filePath) : base(filePath)
		{
		}
	}
}
