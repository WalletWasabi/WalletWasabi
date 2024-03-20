using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Tests.UnitTests.CoinJoinManager;

internal class MockWasabiBackendStatusProvider : IWasabiBackendStatusProvider
{
	public MockWasabiBackendStatusProvider(bool setNullLastResponse)
	{
		LastResponse = setNullLastResponse ? null : new SynchronizeResponse();
	}

	public MockWasabiBackendStatusProvider(SynchronizeResponse? lastResponse)
	{
		LastResponse = lastResponse;
	}

	public SynchronizeResponse? LastResponse { get; set; }
}
