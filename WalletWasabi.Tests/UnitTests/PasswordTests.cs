using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class PasswordTests
	{
		[Fact]
		public void ClipboardCutTest()
		{
			Dictionary<string, string> passwords = new Dictionary<string, string>
			{
				{ "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n", "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯" },
				{ "§'\" + !%/= ()ÖÜÓ'", "§'\" + !%/= ()Ö\ufdff" }
			};

			foreach (var pairs in passwords)
			{
				var original = pairs.Key;
				var desired = pairs.Value;
				var results = PasswordHelper.GetPossiblePasswords(original);
				var foundCorrectPassword = false;

				foreach (var pw in results)
				{
					if (pw == desired)
					{
						foundCorrectPassword = true;
						break;
					}
				}

				Assert.True(foundCorrectPassword);
			}
		}

		[Fact]
		public void FormattingTest()
		{
			string buggy = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯";
			string original = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";

			// Creating a wallet with buggy password.
			var keyManager = KeyManager.CreateNew(out _, Guard.Correct(buggy)); // Every wallet was created with Guard.Correct before.

			// Password will be trimmed inside.
			PasswordHelper.GetMasterExtKey(keyManager, original, out _);

			// This should not throw format exception but pw is not correct.
			Assert.Throws<SecurityException>(() => PasswordHelper.GetMasterExtKey(keyManager, RandomString.Generate(PasswordHelper.MaxPasswordLength), out _));

			// Password should be formatted, before entering here.
			Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, RandomString.Generate(PasswordHelper.MaxPasswordLength + 1), out _));

			// Too long password with extra spaces.
			var badPassword = $"   {RandomString.Generate(PasswordHelper.MaxPasswordLength + 1)}   ";

			// Password should be formatted, before entering here.
			Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, badPassword, out _));

			Assert.True(PasswordHelper.IsTrimable(badPassword, out badPassword));

			// Still too long.
			Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, badPassword, out _));

			Assert.True(PasswordHelper.IsTooLong(badPassword, out badPassword));

			// This should not throw format exception but pw is not correct.
			Assert.Throws<SecurityException>(() => PasswordHelper.GetMasterExtKey(keyManager, badPassword, out _));
		}

		[Fact]
		public void CompatibilityTest()
		{
			string buggy = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯";
			string original = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";

			Assert.Throws<FormatException>(() => PasswordHelper.Guard(buggy));

			Assert.True(PasswordHelper.IsTrimable(buggy, out buggy));

			// Creating a wallet with buggy password.
			var keyManager = KeyManager.CreateNew(out _, buggy);

			Assert.True(PasswordHelper.IsTrimable(original, out original));

			Assert.False(PasswordHelper.TryPassword(keyManager, "falsepassword", out _));

			// This should pass
			Assert.NotNull(PasswordHelper.GetMasterExtKey(keyManager, original, out _));

			Assert.True(PasswordHelper.TryPassword(keyManager, buggy, out string compatiblePasswordNotUsed));
			Assert.Null(compatiblePasswordNotUsed);

			Assert.True(PasswordHelper.TryPassword(keyManager, original, out string compatiblePassword));
			Assert.Equal(buggy, compatiblePassword);
		}
	}
}
