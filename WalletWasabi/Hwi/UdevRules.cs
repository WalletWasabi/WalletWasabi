using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WalletWasabi.Hwi
{
	public static class UdevRules
	{
		public static readonly string[] RuleFileNames =
		{
			"20-hw1.rules",
			"51-coinkite.rules",
			"51-hid-digitalbitbox.rules",
			"51-trezor.rules",
			"51-usb-keepkey.rules",
			"52-hid-digitalbitbox.rules"
		};

		public const string UdevRuleFolderPath = "/etc/udev/rules.d/";

		public static IEnumerable<string> FindMissingUdevs()
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				yield break; // If no Linux then no udevs are missing.
			}

			var dir = new DirectoryInfo(UdevRuleFolderPath);
			var existingRules = new List<string>();
			foreach (var rule in dir.EnumerateFiles())
			{
				existingRules.Add(rule.Name);
			}

			foreach (var rule in RuleFileNames)
			{
				if (existingRules.All(x => x != rule))
				{
					yield return rule;
				}
			}
		}
	}
}
