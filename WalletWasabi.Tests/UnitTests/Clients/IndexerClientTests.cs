using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class IndexerClientTests
{
	[Fact]
	public void ConstantsTests()
	{
		var current = int.Parse(WalletWasabi.Helpers.Constants.BackendMajorVersion);
		var supported = int.Parse(WalletWasabi.Helpers.Constants.ClientSupportBackendVersion);
		Assert.True(supported = current);
	}
}
