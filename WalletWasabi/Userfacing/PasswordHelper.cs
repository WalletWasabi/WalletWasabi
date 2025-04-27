using System.Collections.Generic;
using System.Text;
using NBitcoin;
using System.Security;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Blockchain.Keys;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Extensions;

namespace WalletWasabi.Userfacing;

public static class PasswordHelper
{
	public const int MaxPasswordLength = 150;
	public const string CompatibilityPasswordWarnMessage = "Compatibility passphrase was used! Please consider generating a new wallet to ensure recoverability!";
	public const string MatchingMessage = "Passphrases don't match.";
	public const string WhitespaceMessage = "Leading and trailing white spaces are not allowed!";
	public static readonly string PasswordTooLongMessage = $"Passphrase is too long.";

	public static string[] GetPossiblePasswords(string? originalPassword)
	{
		if (string.IsNullOrEmpty(originalPassword))
		{
			return new[] { "" };
		}

		var buggyClipboard = StringCutIssue(originalPassword);

		List<string> possiblePasswords = new()
		{
			originalPassword,
			buggyClipboard, // Should be here for every OP system. If I create a buggy wallet on OSX and transfer it to other system, it should also work.
			$"{buggyClipboard[0..^1]}\ufffd" // Later I tested the functionality and experienced that the last character replaced by invalid character.
		};

		return possiblePasswords.ToArray();
	}

	private static string StringCutIssue(string text)
	{
		// On OSX Avalonia gets the string from the Clipboard as byte[] and size.
		// The size was mistakenly taken from the size of the original string which is not correct because of the UTF8 encoding.
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		var myString = Encoding.UTF8.GetString(bytes[..text.Length]);
		return text[..myString.Length];
	}

	public static bool IsTooLong(string? password, out string? limitedPassword)
	{
		limitedPassword = password;
		if (password is null)
		{
			return false;
		}

		if (IsTooLong(password.Length))
		{
			limitedPassword = password[..MaxPasswordLength];
			return true;
		}

		return false;
	}

	public static bool IsTooLong(int length)
	{
		return length > MaxPasswordLength;
	}

	public static bool IsTrimmable(string? password, [NotNullWhen(true)] out string? trimmedPassword)
	{
		if (password is { } && password.IsTrimmable())
		{
			trimmedPassword = password.Trim();
			return true;
		}

		trimmedPassword = password;
		return false;
	}

	public static bool TryPassword(KeyManager keyManager, string password, out string? compatibilityPasswordUsed)
	{
		compatibilityPasswordUsed = null;
		try
		{
			GetMasterExtKey(keyManager, password, out compatibilityPasswordUsed);
		}
		catch
		{
			return false;
		}

		return true;
	}

	public static void Guard(string password)
	{
		if (IsTooLong(password, out _)) // Password should be formatted, before entering here.
		{
			throw new FormatException(PasswordTooLongMessage);
		}

		if (IsTrimmable(password, out _)) // Password should be formatted, before entering here.
		{
			throw new FormatException("Leading and trailing white spaces are not allowed!");
		}
	}

	public static ExtKey GetMasterExtKey(KeyManager keyManager, string password, out string? compatibilityPassword)
	{
		password = Helpers.Guard.Correct(password); // Correct the password to ensure compatibility. User will be notified about this through TogglePasswordBox.

		Guard(password);

		compatibilityPassword = null;

		Exception? resultException = null;

		foreach (var pw in GetPossiblePasswords(password))
		{
			try
			{
				ExtKey result = keyManager.GetMasterExtKey(pw);

				// Now the password is OK but if we had SecurityException before then we used a compatibility password.
				if (resultException is not null)
				{
					compatibilityPassword = pw;
					Logger.LogError(CompatibilityPasswordWarnMessage);
				}

				return result;
			}
			catch (SecurityException ex) // Any other exception - let it go.
			{
				resultException = ex;
			}
		}

		throw resultException ?? new InvalidOperationException(); // Throw the last exception - Invalid password.
	}
}
