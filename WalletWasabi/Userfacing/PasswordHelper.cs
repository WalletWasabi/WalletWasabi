using System.Security;

namespace WalletWasabi.Userfacing;

public static class PasswordHelper
{
	public const int MaxPasswordLength = 150;
	public const string MatchingMessage = "Passphrases don't match.";
	public const string WhitespaceMessage = "Leading and trailing white spaces are not allowed!";
	public static readonly string PasswordTooLongMessage = "Passphrase is too long.";

	public static bool IsTooLong(string? password)
	{
		if (password is null)
		{
			return false;
		}

		if (IsTooLong(password.Length))
		{
			return true;
		}

		return false;
	}

	public static bool IsTooLong(int length)
	{
		return length > MaxPasswordLength;
	}

	public static bool IsTrimmable(string? password)
	{
		if (password is { } && password.IsTrimmable())
		{
			return true;
		}

		return false;
	}

	public static bool TryPassword(KeyManager keyManager, string password)
	{
		try
		{
			GetMasterExtKey(keyManager, password);
		}
		catch
		{
			return false;
		}

		return true;
	}

	public static void AssertCorrectPassword(string password)
	{
		if (IsTooLong(password))
		{
			throw new FormatException(PasswordTooLongMessage);
		}

		if (IsTrimmable(password))
		{
			throw new FormatException("Leading and trailing white spaces are not allowed!");
		}
	}

	public static ExtKey GetMasterExtKey(KeyManager keyManager, string password)
	{
		AssertCorrectPassword(password);

		try
		{
			return keyManager.GetMasterExtKey(password);
		}
		catch (SecurityException)
		{
			throw;
		}

		throw new InvalidOperationException(); // The password is invalid.
	}
}
