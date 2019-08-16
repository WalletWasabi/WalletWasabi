using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using Xunit;

namespace WalletWasabi.Tests
{
	public class PasswordTests
	{
		[Fact]
		public void PasswordClipboardCutTest()
		{
			string original = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";

			string desired = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯";

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

		[Fact]
		public void GetMasterExtKeyTest()
		{
			string buggy = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯";
			string original = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";

			var keyManager = KeyManager.CreateNew(out _, buggy); // Creating a wallet with buggy password.

			Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, original, out _)); // Password should be formatted, before entering here.

			// This should not throw format exception.
			Assert.Throws<SecurityException>(() => PasswordHelper.GetMasterExtKey(keyManager, RandomString.Generate(PasswordHelper.MaxPasswordLength), out _));

			Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager,RandomString.Generate(PasswordHelper.MaxPasswordLength +1 ),out _)); // Password should be formatted, before entering here.

		}
	}
}
