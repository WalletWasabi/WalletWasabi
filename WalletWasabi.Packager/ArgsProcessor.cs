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
	public bool IsOnlyBinariesMode() => IsOneOf("onlybinaries");

	public bool IsContinuousDeliveryMode() => IsOneOf("cdelivery");

	/// <summary>Only signs macOS binaries and terminates.</summary>
	public bool IsMacSigning() => IsOneOf("sign");

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
}
