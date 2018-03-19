using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	[CollectionDefinition("Shared collection")]
	public class SharedCollection : ICollectionFixture<SharedFixture>
	{
		// This class has no code, and is never created. Its purpose is simply
		// to be the place to apply [CollectionDefinition] and all the
		// ICollectionFixture<> interfaces.
	}
}
