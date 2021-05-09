using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Filters
{
	public class ExceptionTranslateAttribute : ExceptionFilterAttribute
	{
		public override void OnException(ExceptionContext context)
		{
			var exception = context.Exception.InnerException ?? context.Exception;
			var exceptionMessage = exception.Message;

			context.Result = new JsonResult(
				exception switch
				{
					WabiSabiProtocolException e => new Error(
						Type: "wabisabi-protocol-violation",
						ErrorCode: e.ErrorCode.ToString(),
						Description: e.Message),
					Exception e => new Error(
						Type: "unknown",
						ErrorCode: "0",
						Description: e.Message)
				}
			)
			{
				StatusCode = (int)HttpStatusCode.InternalServerError
			};
		}
	}
}