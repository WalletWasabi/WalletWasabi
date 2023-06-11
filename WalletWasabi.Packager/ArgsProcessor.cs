using System.Linq;

namespace WalletWasabi.Packager;

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

	/// <summary>Builds Wasabi Wallet binaries for supported platforms to be compared then with the official binaries, and terminates.</summary>
	/// <seealso href="https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/DeterministicBuildGuide.md"/>
	public bool IsOnlyBinariesMode() => IsOneOf("onlybinaries") || Args is null || Args.Length == 0;

	public bool IsContinuousDeliveryMode() => IsOneOf("cdelivery");

	public bool IsPublish() => IsOneOf("publish");

	public bool IsSign() => IsOneOf("sign");

	public bool IsGeneratePrivateKey() => IsOneOf("generatekeys");

	private bool IsOneOf(params string[] values)
	{
		foreach (var value in values)
		{
			foreach (var arg in Args)
			{
				if (arg.Trim().TrimStart('-').Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}

		return false;
	}

	public (string AppleId, string Password) GetAppleIdAndPassword()
	{
		string appleId = "";
		string password = "";

		try
		{
			var appleidArg = Args.First(a => a.Contains("appleid", StringComparison.InvariantCultureIgnoreCase));
			var parameters = appleidArg.Split("=")[1];
			var idAndPassword = parameters.Split(":");
			appleId = idAndPassword[0];
			password = idAndPassword[1];
		}
		catch (Exception)
		{
		}

		return (appleId, password);
	}
}
