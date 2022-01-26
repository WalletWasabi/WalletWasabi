using System.Threading.Tasks;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class PreventSleepTests
{
	[Fact]
	public async Task ProlongSystemAwakeCanBeExecutedAsync()
	{
		await EnvironmentHelpers.ProlongSystemAwakeAsync();
	}
}
