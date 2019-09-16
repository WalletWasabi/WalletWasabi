using System;

namespace System
{
	public static class VersionExtensions
	{
		public static string ToVersionString(this Version version)
		{
			return version.ToString(4);
		}

		public static Version ToVersion(this Version version, int fieldCount)
		{
			return new Version(version.ToString(fieldCount));
		}
	}
}
