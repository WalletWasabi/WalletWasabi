#nullable enable

using System;

namespace WalletWasabi.Packager
{
	/// <summary>
	/// Class for processing program's command line arguments.
	/// </summary>
	public class ArgsProcessor
	{
		public ArgsProcessor(string[] args)
		{
			Args = args;
		}

		public string[] Args { get; }

		public bool IsReduceOnionsMode()
		{
			foreach (var arg in Args)
			{
				string value = arg.Trim().TrimStart('-');

				if (value.Equals("reduceonions", StringComparison.OrdinalIgnoreCase)
					|| value.Equals("reduceonion", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public bool IsOnlyCreateDigestsMode()
		{
			foreach (var arg in Args)
			{
				string value = arg.Trim().TrimStart('-');

				if (value.Equals("onlycreatedigests", StringComparison.OrdinalIgnoreCase)
					|| value.Equals("onlycreatedigest", StringComparison.OrdinalIgnoreCase)
					|| value.Equals("onlydigests", StringComparison.OrdinalIgnoreCase)
					|| value.Equals("onlydigest", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public bool IsOnlyBinariesMode()
		{
			foreach (var arg in Args)
			{
				if (arg.Trim().TrimStart('-').Equals("onlybinaries", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public bool IsGetOnionsMode()
		{
			foreach (var arg in Args)
			{
				string value = arg.Trim().TrimStart('-');

				if (value.Equals("getonions", StringComparison.OrdinalIgnoreCase)
					|| value.Equals("getonion", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
