using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Client.Rpc;
using WalletWasabi.Rpc;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class RpcTests
{
	public static TheoryData<string, string> RequestResponse
	{
		get
		{
			var result = new TheoryData<string, string>
			{
				{
					// Invalid (broken) request
					"""{"jsonrpc":"2.0","method":"substract","params":[],"id","1"}""",
					"""{"jsonrpc":"2.0","error":{"code":-32700,"message":"Parse error"},"id":null}"""
				},
				{
					// Invalid request (missing jsonrpc)
					"""{"method":"substract","params":[42,23],"id":"1"}""",
					"""{"jsonrpc":"2.0","result":19,"id":"1"}"""
				},
				{
					// Invalid request (missing method)
					"""{"jsonrpc":"2.0","params":["[42, 23]"],"id":"1"}""",
					"""{"jsonrpc":"2.0","error":{"code":-32700,"message":"Parse error"},"id":null}"""
				},
				{
					// Invalid request (wrong number of arguments)
					"""{"jsonrpc":"2.0","method":"substract","params":{"subtrahend":23},"id":"3"}""",
					"""{"jsonrpc":"2.0","error":{"code":-32602,"message":"A value for the 'minuend' is missing."},"id":"3"}"""
				},
				{
					// Invalid request (wrong number of arguments)
					"""{"jsonrpc":"2.0","method":"substract","params":[23],"id":"3"}""",
					"""{"jsonrpc":"2.0","error":{"code":-32602,"message":"2 parameters were expected but 1 were received."},"id":"3"}"""
				},
				{
					// Valid request with params by order
					"""{"jsonrpc":"2.0","method":"substract","params":[42,23],"id":"1"}""",
					"""{"jsonrpc":"2.0","result":19,"id":"1"}"""
				},
				{
					// Valid request with params by name
					"""{"jsonrpc":"2.0","method":"substract","params":{"minuend":42,"subtrahend":23},"id":"2"}""",
					"""{"jsonrpc":"2.0","result":19,"id":"2"}"""
				},
				{
					// Valid request (Notification)
					"""{"jsonrpc":"2.0","method":"substract","params":[42,23],"id":null}""",
					""
				},
				{
					// Valid request for void procedure
					"""{"jsonrpc":"2.0","method":"writelog","params":["blah blah blah"],"id":"log-id-01"}""",
					"""{"jsonrpc":"2.0","result":null,"id":"log-id-01"}"""
				},
				{
					// Valid request for async procedure with cancellation token
					"""{"jsonrpc":"2.0","method":"format","params":["c:"],"id":"1"}""",
					"""{"jsonrpc":"2.0","result":null,"id":"1"}"""
				},
				{
					// Valid request but internal server error
					"""{"jsonrpc":"2.0","method":"fail","params":["c:"],"id":"1"}""",
					"""{"jsonrpc":"2.0","error":{"code":-32603,"message":"the error"},"id":"1"}"""
				},
				{
					// Valid request to async method
					"""{"jsonrpc":"2.0","method":"substractasync","params":{"minuend":42,"subtrahend":23},"id":"7"}""",
					"""{"jsonrpc":"2.0","result":19,"id":"7"}"""
				},
			};

			return result;
		}
	}

	[Theory]
	[MemberData(nameof(RequestResponse))]
	public async Task ParsingRequestTestsAsync(string request, string expectedResponse)
	{
		var handler = new JsonRpcRequestHandler<TestableRpcService>(new TestableRpcService(), Network.Main);

		var response = await handler.HandleAsync("", request, CancellationToken.None);
		Assert.Equal(expectedResponse, response);
	}

	[Fact]
	public void BuildTransactionWithFees()
	{
		var service = new WasabiJsonRpcService(null!);
		var paymentInfo = new PaymentInfo
		{
			Amount = Money.Coins(1),
			Sendto = new Destination.Loudly(BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main).ScriptPubKey),
			Label = "Cesar"
		};

		void BuildTransaction(int? feeTarget = null, decimal? feeRate = null) =>
			service.BuildTransaction(new[] { paymentInfo }, [], feeTarget, feeRate);

		// No fee information is provided
		Assert.Throws<ArgumentException>(() => BuildTransaction());

		// Invalid feeTarget (out of range)
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeTarget: -4));
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeTarget: 0));
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeTarget: 2000));

		// Invalid feeRate (out of range)
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeRate: 0));
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeRate: 20_000));

		// Contradictory fee information (both feeRate and feeTarget are present)
		Assert.Throws<ArgumentException>(() => BuildTransaction(feeRate: 20, feeTarget: 8));
		Assert.Throws<InvalidOperationException>(() => BuildTransaction(feeRate: 20));
		Assert.Throws<InvalidOperationException>(() => BuildTransaction(feeTarget: 1008));
	}
}
