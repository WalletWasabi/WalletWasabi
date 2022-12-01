using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Filters;

public class ExceptionTranslateFilter : ExceptionFilterAttribute
{
	public override void OnException(ExceptionContext context)
	{
		var exception = context.Exception.InnerException ?? context.Exception;

		Logger.LogError(exception);

		context.Result = exception switch
		{
			Exception e => new JsonResult(new Error(
				Description: e.Message
			))
			{
				StatusCode = (int)HttpStatusCode.InternalServerError
			},
			_ => new StatusCodeResult((int)HttpStatusCode.InternalServerError)
		};
	}
}
