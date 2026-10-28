using System.Security;
using Xunit;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Userfacing;
using NBitcoin;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

/// <summary>
/// Tests for <see cref="PasswordHelper"/>.
/// </summary>
public class PasswordHelperTests
{
	[Fact]
	public void FormattingTest()
	{
		string password = "Hello";

		// Creating a wallet with buggy password.
		var keyManager = KeyManager.CreateNew(out _, password, Network.Main);

		// Should not throw.
		PasswordHelper.GetMasterExtKey(keyManager, password);

		// The correct password is accepted.
		Assert.True(PasswordHelper.TryPassword(keyManager, password));

		// The correct password with additional whitespace must be rejected.
		Assert.False(PasswordHelper.TryPassword(keyManager, $"{password} "));

		// This should not throw format exception but the password is not correct.
		Assert.Throws<SecurityException>(() => PasswordHelper.GetMasterExtKey(keyManager, RandomString.AlphaNumeric(PasswordHelper.MaxPasswordLength)));

		// Password should be formatted, before entering here.
		Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, RandomString.AlphaNumeric(PasswordHelper.MaxPasswordLength + 1)));

		// Too long password with extra spaces.
		var badPassword = $"   {RandomString.AlphaNumeric(PasswordHelper.MaxPasswordLength + 1)}   ";

		// Password should be formatted, before entering here.
		Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, badPassword));

		Assert.True(PasswordHelper.IsTrimmable(badPassword));

		// Still too long.
		Assert.Throws<FormatException>(() => PasswordHelper.GetMasterExtKey(keyManager, badPassword));

		Assert.True(PasswordHelper.IsTooLong(badPassword));
	}
}
