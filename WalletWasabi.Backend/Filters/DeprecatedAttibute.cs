using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WalletWasabi.Backend.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class DeprecatedAttribute : Attribute, IResourceFilter
{
	public void OnResourceExecuting(ResourceExecutingContext context)
	{
			context.Result = new ContentResult()
			{
				Content = "The Wasabi Wallet v1 CoinJoin API has been deprecated.",
				ContentType = "text/plain",
				StatusCode = (int) HttpStatusCode.NotImplemented
			};
	}

	public void OnResourceExecuted(ResourceExecutedContext context)
	{
		// We have to do nothing here.
	}
}
