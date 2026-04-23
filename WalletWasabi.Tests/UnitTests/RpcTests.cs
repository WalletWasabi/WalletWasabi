using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Daemon.Rpc;
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
					Request("1", "substract").Replace("\"id\":", "\"id\","),
					Error(null!, -32700, "Parse error")
				},
				{
					// Invalid (missing jsonrpc) request
					Request("1", "substract", 42, 23).Replace("\"jsonrpc\":\"2.0\",", ""),
					Ok("1", 19)
				},
				{
					// Invalid (missing method) request
					Request("1", "", "[42, 23]").Replace("\"method\":\"\",", ""),
					Error(null!, -32700, "Parse error")
				},
				{
					// Invalid (wrong number of arguments) request
					Request("3", "substract", new { subtrahend = 23 }),
					Error("3", -32602, "A value for the 'minuend' is missing.")
				},
				{
					// Invalid (wrong number of arguments) request
					Request("3", "substract", 23),
					Error("3", -32602, "2 parameters were expected but 1 were received.")
				},
				{
					// Valid request with params by order
					Request("1", "substract", 42, 23),
					Ok("1", 19)
				},
				{
					// Valid request with params by name
					Request("2", "substract", new { minuend = 42, subtrahend = 23 }),
					Ok("2", 19)
				},
				{
					// Valid request (Notification)
					Request(null!, "substract", 42, 23),
					""
				},
				{
					// Valid request for void procedure
					Request("log-id-01", "writelog", "blah blah blah"),
					Ok("log-id-01", null!)
				},
				{
					// Valid request for async procedure with cancellation token
					Request("1", "format", "c:"),
					Ok("1", null!)
				},
				{
					// Valid request but internal server error
					Request("1", "fail", "c:"),
					Error("1", -32603, "the error")
				},
				{
					// Valid request to async method
					Request("7", "substractasync", new { minuend = 42, subtrahend = 23 }),
					Ok("7", 19)
				}
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

	private static string Request(string id, string methodName, params object[] parameters)
	{
		return ToJson(new
		{
			jsonrpc = "2.0",
			method = methodName,
			@params = parameters.Length == 1 && (parameters[0].GetType().IsClass && parameters[0] is not string) ? parameters[0] : parameters,
			id
		});
	}

	private static string Ok(string id, object content)
	{
		return ToJson(new
		{
			jsonrpc = "2.0",
			result = content,
			id
		});
	}

	private static string Error(string id, int code, string message)
	{
		return ToJson(new
		{
			jsonrpc = "2.0",
			error = new { code, message },
			id
		});
	}

	private static string ToJson(object o)
	{
		var options = new JsonSerializerOptions()
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		return JsonSerializer.Serialize(o, options);
	}
}
