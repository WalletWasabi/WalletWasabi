using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
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

		// Cannot reproduce this bug in a reasonable way with any string. Leave this here for the record.
		//[Fact]
		//public void PasswordClipboardFirstPasteTest()
		//{
		//	// Original text
		//	//"    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";

		//	// First paste (40 leading spaces)
		//	//"\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0\\0�ÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯"

		//	string original = "    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n";
		//	string desired = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0�ÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯";
		//	var results = PasswordHelper.GetPossibleCompatiblityPasswords(original);
		//	var foundCorrectPassword = false;

		//	foreach (var pw in results)
		//	{
		//		if (pw == desired)
		//		{
		//			foundCorrectPassword = true;
		//			break;
		//		}
		//	}

		//	Assert.True(foundCorrectPassword);
		//}
	}
}
