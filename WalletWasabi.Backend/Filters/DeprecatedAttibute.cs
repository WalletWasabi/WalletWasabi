using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WalletWasabi.Affiliation;
using WabiSabi.Crypto;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class DeprecatedAttribute : ActionFilterAttribute
{
	public override void OnActionExecuting(ActionExecutingContext filterContext)
	{
		filterContext.Result = new ContentResult()
		{
			Content = "The API has been deprecated",
			ContentType = "text/plain",
			StatusCode = (int) HttpStatusCode.NotImplemented
		};
	}
}
