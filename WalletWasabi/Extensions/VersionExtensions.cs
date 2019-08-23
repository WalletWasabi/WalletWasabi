using System;

namespace System
{
	public static class VersionExtensions
	{
		public static string ToVersionString(this Version version)
		{
#if RELEASE
			return version.ToString(3);
#else
			return version.ToString(4);
#endif
		}
	}
}
