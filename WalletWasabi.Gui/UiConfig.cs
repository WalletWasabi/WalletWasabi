using Avalonia.Controls;
using Newtonsoft.Json;
using System.ComponentModel;
using WalletWasabi.Bases;
using WalletWasabi.Gui.Converters;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UiConfig : ConfigBase
	{
		private bool _lurkingWifeMode;
		private bool _lockScreenActive;
		private string _lockScreenPinHash;
		private bool _isCustomFee;
		private bool _isCustomChangeAddress;
		private bool _autocopy;

		public UiConfig() : base()
		{
		}

		public UiConfig(string filePath) : base(filePath)
		{
		}

		[JsonProperty(PropertyName = nameof(WindowState))]
		[JsonConverter(typeof(WindowStateAfterStartJsonConverter))]
		public WindowState WindowState { get; internal set; } = WindowState.Maximized;

		[DefaultValue(2)]
		[JsonProperty(PropertyName = nameof(FeeTarget), DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FeeTarget { get; internal set; }

		[DefaultValue(0)]
		[JsonProperty(PropertyName = nameof(FeeDisplayFormat), DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FeeDisplayFormat { get; internal set; }

		[DefaultValue("")]
		[JsonProperty(PropertyName = nameof(LastActiveTab), DefaultValueHandling = DefaultValueHandling.Populate)]
		public string LastActiveTab { get; internal set; }

		[DefaultValue(true)]
		[JsonProperty(PropertyName = nameof(Autocopy), DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Autocopy
		{
			get => _autocopy;
			set => RaiseAndSetIfChanged(ref _autocopy, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = nameof(IsCustomFee), DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsCustomFee
		{
			get => _isCustomFee;
			set => RaiseAndSetIfChanged(ref _isCustomFee, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = nameof(IsCustomChangeAddress), DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsCustomChangeAddress
		{
			get => _isCustomChangeAddress;
			set => RaiseAndSetIfChanged(ref _isCustomChangeAddress, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = nameof(LurkingWifeMode), DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LurkingWifeMode
		{
			get => _lurkingWifeMode;
			set => RaiseAndSetIfChanged(ref _lurkingWifeMode, value);
		}

		[DefaultValue(false)]
		[JsonProperty(PropertyName = nameof(LockScreenActive), DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool LockScreenActive
		{
			get => _lockScreenActive;
			set => RaiseAndSetIfChanged(ref _lockScreenActive, value);
		}

		[DefaultValue("")]
		[JsonProperty(PropertyName = nameof(LockScreenPinHash), DefaultValueHandling = DefaultValueHandling.Populate)]
		public string LockScreenPinHash
		{
			get => _lockScreenPinHash;
			set => RaiseAndSetIfChanged(ref _lockScreenPinHash, value);
		}
	}
}
