using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class LibUsbTests
{
	/// <summary>The bundled library for the platform and the architecture of the process loads and answers.</summary>
	[Fact]
	public void LoadsTheBundledLibrary()
	{
		Assert.Equal("LIBUSB_ERROR_TIMEOUT", LibUsb.ErrorName(LibUsb.ErrorTimeout));
	}
}
