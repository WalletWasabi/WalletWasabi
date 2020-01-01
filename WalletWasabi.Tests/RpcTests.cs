using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Gui.Rpc;
using Xunit;

namespace WalletWasabi.Tests
{

	public class RpcTests
	{
		[Theory]
		[MemberData(nameof(RequestResponse))]
		public async Task ParsingRequestTestsAsync(string request, string expectedResponse)
		{
			var handler = new JsonRpcRequestHandler<TesteableRpcService>(new TesteableRpcService());

			var response = await handler.HandleAsync(request, CancellationToken.None);
			Assert.Equal(expectedResponse, response);
		}

		public static IEnumerable<object[]> RequestResponse =>
			new[]
			{
				new[] {
					"Invalid (broken) request",
					Request("1", "substract").Replace("\"id\":", "\"id\","),
					Error(null, -32700, "Parse error")},
				new[] {
					"Invalid (missing jsonrpc) request",
					Request("1", "substract", 42, 23).Replace("\"jsonrpc\":\"2.0\",", ""),
					Error(null, -32700, "Parse error")},
				new[] {
					"Invalid (missing method) request",
					Request("1", "", "[42, 23]").Replace("\"method\":\"\",", ""),
					Error(null, -32700, "Parse error")},
				new[] {
					"Invalid (wrong number of arguments) request",
					Request("3", "substract", new { subtrahend = 23}),
					Error("3", -32602, "A value for the 'minuend' is missing.")},
				new[] {
					"Invalid (wrong number of arguments) request",
					Request("3", "substract", 23 ),
					Error("3", -32602, "2 parameters were expected but 1 were received.")},
				new[] {
					"Valid request with params by order",
					Request("1", "substract", 42, 23),
					Ok("1", 19)},
				new[] {
					"Valid request with params by name",
					Request("2", "substract", new { minuend = 42, subtrahend = 23}),
					Ok("2", 19)},
				new[] {
					"Valid request (Notification)",
					Request(null, "substract", 42, 23),
					string.Empty},
				new[] {
					"Valid request for void procedure",
					Request("log-id-01", "writelog", "blah blah blah" ),
					Ok("log-id-01", null)},
				new[] {
					"Valid request for async procedure with cancellation token",
					Request("1", "format", "c:" ),
					Ok("1", null)},
				new[] {
					"Valid request but internal server error",
					Request("1", "fail", "c:" ),
					Error("1", -32603, "the error")},
				new[] {
					"Valid request to async method",
					Request("7", "substractasync", new { minuend = 42, subtrahend = 23}),
					Ok("7", 19)}
		}.Select(x => x.Skip(1).ToArray());

		private static string Request(string id, string methodName, params object[] parameters)
		{
			return ToJson(new
			{
				jsonrpc = "2.0",
				method = methodName,
				@params = parameters.Length == 1 && (parameters[0].GetType().IsClass && !(parameters[0] is string)) ? parameters[0] : parameters,
				id = id
			});
		}

		private static string Ok(string id, object content)
		{
			return ToJson(new
			{
				jsonrpc = "2.0",
				result = content,
				id = id
			});
		}

		private static string Error(string id, int code, string message)
		{
			return ToJson(new
			{
				jsonrpc = "2.0",
				error = new { code, message },
				id = id
			});
		}

		private static string ToJson(object o)
		{
			return JsonConvert.SerializeObject(o, new JsonSerializerSettings
			{
				DefaultValueHandling = DefaultValueHandling.Ignore
			});
		}
	}
}