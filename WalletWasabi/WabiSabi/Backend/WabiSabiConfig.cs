using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class WabiSabiConfig : ConfigBase
	{
		public WabiSabiConfig() : base()
		{
		}

		public WabiSabiConfig(string filePath) : base(filePath)
		{
		}

		[DefaultValue(Constants.OneDayConfirmationTarget)]
		[JsonProperty(PropertyName = "ConfirmationTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ConfirmationTarget { get; set; }
	}
}
