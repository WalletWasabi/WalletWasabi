using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration
{
	/// <summary>
	/// This class has no code, and is never created. Its purpose is simply
	//  to be the place to apply [CollectionDefinition].
	/// </summary>
	/// <seealso href="https://xunit.net/docs/shared-context#collection-fixture"/>
	[CollectionDefinition("Serial unit tests collection", DisableParallelization = true)]
	public class SerialCollectionDefinition
	{
	}
}
