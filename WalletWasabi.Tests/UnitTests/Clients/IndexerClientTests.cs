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
		var supported = int.Parse(WalletWasabi.Helpers.Constants.ClientSupportBackendVersionMin);
		var current = int.Parse(WalletWasabi.Helpers.Constants.BackendMajorVersion);

		Assert.True(supported == current);
	}
}
