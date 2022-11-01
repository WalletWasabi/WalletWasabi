using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WalletWasabi.Fluent.Desktop.Extensions;

public static class UriSchemeExtension
{
	private const string UriScheme = "bitcoin";
	private const string FriendlyName = "Bitcoin payments";

	public static void RegisterUriSchemeWindows()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return;
		}

		using var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme);

		var applicationLocation = Environment.ProcessPath;

		key.SetValue("", "URL:" + FriendlyName);
		key.SetValue("URL Protocol", "");

		using var defaultIcon = key.CreateSubKey("DefaultIcon");
		using var commandKey = key.CreateSubKey(@"shell\open\command");

		defaultIcon.SetValue("", applicationLocation + ",1");
		commandKey.SetValue("", $@"""{applicationLocation}"" -url ""%1""");
	}

	public static string? GetUrlFromArgs(string[] args)
	{
		var countOfUrls = args.Select(x => x.Trim() == "-url").Count();
		if (countOfUrls > 0)
		{
			var urlIndex = args.Where(x => x == "-url").Select((x, i) => i).First() + 1;
			var url = args[urlIndex].Trim('"');
			if (Uri.TryCreate(url, UriKind.Absolute, out var uriObj))
			{
				if (uriObj.Scheme == UriScheme)
				{
					return uriObj.ToString();
				}
			}
		}

		return null;
	}

}
