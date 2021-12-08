using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabiClientLibrary.Middlewares;

public class RequestLoggerMiddleware
{
	private readonly RequestDelegate _next;

	public RequestLoggerMiddleware(RequestDelegate next, IConfiguration config)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext httpContext)
	{
		await LogRequestAsync(httpContext.Request);
		await _next(httpContext);
	}

	private static async Task LogRequestAsync(HttpRequest httpRequest)
	{
		Logger.LogInfo($"Method: {httpRequest.Path}");
		Logger.LogInfo($"Request body: {await GetRequestBodyAsync(httpRequest)}");
	}

	private static async Task<string> GetRequestBodyAsync(HttpRequest httpRequest)
	{
		httpRequest.EnableBuffering();

		using StreamReader streamReader = new(httpRequest.Body, leaveOpen: true);
		string requestBody = await streamReader.ReadToEndAsync();
		httpRequest.Body.Position = 0;
		return requestBody;
	}
}
