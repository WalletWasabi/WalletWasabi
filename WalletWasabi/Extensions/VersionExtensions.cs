using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Extensions
{
	public static class VersionExtensions
	{
		public static Version ToVersion(this Version version, int fieldCount)
		{
			return Version.Parse(version.ToString(fieldCount));
		}
	}
}
