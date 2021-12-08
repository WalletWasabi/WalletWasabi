using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabiClientLibrary.Middlewares;

public class ResponseLoggerMiddleware
{
	private readonly RequestDelegate _next;

	public ResponseLoggerMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		Stream originalStream = context.Response.Body;
		using MemoryStream memoryStream = new();
		context.Response.Body = memoryStream;

		await _next(context);

		await LogResponseAsync(context.Response);

		await memoryStream.CopyToAsync(originalStream);
	}

	private static async Task<string> GetResponseBodyAsync(HttpResponse httpResponse)
	{
		Stream body = httpResponse.Body;
		body.Seek(0, SeekOrigin.Begin);
		using StreamReader streamReader = new(body, leaveOpen: true);
		string responseBody = await streamReader.ReadToEndAsync();
		body.Seek(0, SeekOrigin.Begin);
		return responseBody;
	}

	private static async Task LogResponseAsync(HttpResponse response)
	{
		Logger.LogInfo($"Response body: {await GetResponseBodyAsync(response)}");
	}
}
