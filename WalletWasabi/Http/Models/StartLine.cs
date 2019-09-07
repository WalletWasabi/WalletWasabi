using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using static WalletWasabi.Http.Constants;

namespace WalletWasabi.Http.Models
{
	public abstract class StartLine
	{
		public HttpProtocol Protocol { get; }

		protected StartLine(HttpProtocol protocol)
		{
			Protocol = protocol;
		}

		public static string[] GetParts(string startLineString)
		{
			var trimmed = Guard.NotNullOrEmptyOrWhitespace(nameof(startLineString), startLineString, trim: true);
			return trimmed.Split(SP, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
		}
	}
}
