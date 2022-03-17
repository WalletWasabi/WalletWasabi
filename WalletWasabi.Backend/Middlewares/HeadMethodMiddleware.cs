using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace WalletWasabi.Backend.Middlewares;

/// <summary>
/// Source: https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
/// Source: https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
/// </summary>
public class HeadMethodMiddleware
{
	#region Fields

	private readonly RequestDelegate _next;

	#endregion Fields

	#region Constructor

	public HeadMethodMiddleware(RequestDelegate next)
	{
		_next = next ?? throw new ArgumentNullException(nameof(next));
	}

	#endregion Constructor

	#region Methods

	public async Task InvokeAsync(HttpContext context)
	{
		bool methodSwitched = false;

		if (HttpMethods.IsHead(context.Request.Method))
		{
			methodSwitched = true;

			context.Request.Method = HttpMethods.Get;
			context.Response.Body = Stream.Null;
		}
		try
		{
			await _next(context);
		}
		catch (Exception)
		{
		// https://stackoverflow.com/a/41123756/1844347
			// Shallow the exception.
		}

		if (methodSwitched)
		{
			context.Request.Method = HttpMethods.Head;
		}
	}

	#endregion Methods
}
