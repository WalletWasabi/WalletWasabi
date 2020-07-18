using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Packager
{
	public class ArgsProcessor
	{
		public bool IsReduceOnionsMode(string[] args)
		{
			bool getOnions = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("reduceonions", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("reduceonion", StringComparison.OrdinalIgnoreCase))
					{
						getOnions = true;
						break;
					}
				}
			}

			return getOnions;
		}

		public bool IsOnlyCreateDigestsMode(string[] args)
		{
			bool onlyCreateDigests = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("onlycreatedigests", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlycreatedigest", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlydigests", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("onlydigest", StringComparison.OrdinalIgnoreCase))
					{
						onlyCreateDigests = true;
						break;
					}
				}
			}

			return onlyCreateDigests;
		}

		public bool IsOnlyBinariesMode(string[] args)
		{
			bool onlyBinaries = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("onlybinaries", StringComparison.OrdinalIgnoreCase))
					{
						onlyBinaries = true;
						break;
					}
				}
			}

			return onlyBinaries;
		}

		public bool IsGetOnionsMode(string[] args)
		{
			bool getOnions = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					if (arg.Trim().TrimStart('-').Equals("getonions", StringComparison.OrdinalIgnoreCase)
						|| arg.Trim().TrimStart('-').Equals("getonion", StringComparison.OrdinalIgnoreCase))
					{
						getOnions = true;
						break;
					}
				}
			}

			return getOnions;
		}
	}
}