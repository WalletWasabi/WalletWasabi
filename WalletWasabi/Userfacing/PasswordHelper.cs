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

	/// <remarks>
	/// Note that the operation is slow because of <see cref="GetMasterExtKey(KeyManager, string)"/>. Do not run on the UI thread.
	/// </remarks>
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
			throw new FormatException(WhitespaceMessage);
		}
	}

	public static ExtKey GetMasterExtKey(KeyManager keyManager, string password)
	{
		AssertCorrectPassword(password);
		return keyManager.GetMasterExtKey(password);
	}
}
