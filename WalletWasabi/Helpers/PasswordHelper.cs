using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.KeyManagement;
using NBitcoin;
using System.Security;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Helpers
{
	public static class PasswordHelper
	{
		public const int MaxPasswordLength = 150;
		public const string CompatibilityPasswordWarnMessage = "Compatibility password was used! Please consider generating a new wallet to ensure recoverability!";
		public static readonly string PasswordTooLongMessage = $"Password is too long (Max {MaxPasswordLength} characters).";
		public const string TrimWarnMessage = "Leading and trailing white spaces will be removed!";

		public static string[] GetPossiblePasswords(string originalPassword)
		{
			var buggyClipboard = StringCutIssue(originalPassword);
			List<string> possiblePasswords = new List<string>()
			{
				originalPassword,
				buggyClipboard, // Should be here for every OP system. If I created a buggy wallet on OSX and transfered it to other system, it should also work.
				buggyClipboard.Substring(0,buggyClipboard.Length-1) // Later I tested the functionality and experienced that the last character missing. To ensure compatibility I added another option instead of fixing the prev version.
			};

			return possiblePasswords.ToArray();
		}

		private static string StringCutIssue(string text)
		{
			// On OSX Avalonia gets the string from the Clipboard as byte[] and size.
			// The size was mistakenly taken from the size of the original string which is not correct because of the UTF8 encoding.
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			var myString = Encoding.UTF8.GetString(bytes.Take(text.Length).ToArray());
			return text.Substring(0, myString.Length);
		}

		public static bool IsTooLong(string password, out string limitedPassword)
		{
			limitedPassword = password;
			if (password is null)
			{
				return false;
			}

			if (IsTooLong(password.Length))
			{
				limitedPassword = password.Substring(0, MaxPasswordLength);
				return true;
			}

			return false;
		}

		public static bool IsTooLong(int length)
		{
			return length > MaxPasswordLength;
		}

		public static bool IsTrimable(string password, out string trimmedPassword)
		{
			trimmedPassword = password;
			if (password is null)
			{
				return false;
			}

			var beforeTrim = password.Length;

			trimmedPassword = password.Trim();

			if (beforeTrim != trimmedPassword.Length)
			{
				return true;
			}

			return false;
		}

		public static bool TryPassword(KeyManager keyManager, string password, out string compatibilityPasswordUsed)
		{
			compatibilityPasswordUsed = null;
			try
			{
				GetMasterExtKey(keyManager, password, out compatibilityPasswordUsed);
			}
			catch (Exception)
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

			if (IsTrimable(password, out _)) // Password should be formatted, before entering here.
			{
				throw new FormatException("Leading and trailing white spaces are not enabled!");
			}
		}

		public static ExtKey GetMasterExtKey(KeyManager keyManager, string password, out string compatiblityPassword)
		{
			password = Helpers.Guard.Correct(password); // Correct the password to ensure compatiblity. User will be notified about this through TogglePasswordBox.

			Guard(password);

			compatiblityPassword = null;

			Exception resultException = null;

			foreach (var pw in GetPossiblePasswords(password))
			{
				try
				{
					ExtKey result = keyManager.GetMasterExtKey(pw);

					if (resultException != null) // Now the password is OK but if we had SecurityException before than we used a cmp password.
					{
						compatiblityPassword = pw;
						Logger.LogError<KeyManager>(CompatibilityPasswordWarnMessage);
					}
					return result;
				}
				catch (SecurityException ex) // Any other exception - let it go.
				{
					resultException = ex;
				}
			}

			if (resultException is null) // This mustn't be null.
			{
				throw new InvalidOperationException();
			}

			throw resultException; // Throw the last exception - Invalid password.
		}

		public static ErrorDescriptors ValidatePassword(string password)
		{
			var errors = new ErrorDescriptors();

			if (IsTrimable(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Warning, TrimWarnMessage));
			}

			if (IsTooLong(password, out _))
			{
				errors.Add(new ErrorDescriptor(ErrorSeverity.Error, PasswordTooLongMessage));
			}

			return errors;
		}
	}
}
