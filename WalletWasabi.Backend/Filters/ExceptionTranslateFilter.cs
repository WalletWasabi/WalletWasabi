using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Filters;

public class ExceptionTranslateAttribute : ExceptionFilterAttribute
{
	public override void OnException(ExceptionContext context)
	{
		var exception = context.Exception.InnerException ?? context.Exception;

		context.Result = exception switch
		{
			WabiSabiProtocolException e => new JsonResult(new Error(
				Type: ProtocolConstants.ProtocolViolationType,
				ErrorCode: e.ErrorCode.ToString(),
				Description: e.Message,
				ExceptionData: e.ExceptionData ?? EmptyExceptionData.Instance))
			{
				StatusCode = (int)HttpStatusCode.InternalServerError
			},
			WabiSabiCryptoException e => new JsonResult(new Error(
				Type: ProtocolConstants.ProtocolViolationType,
				ErrorCode: WabiSabiProtocolErrorCode.CryptoException.ToString(),
				Description: e.Message,
				ExceptionData: EmptyExceptionData.Instance))
			{
				StatusCode = (int)HttpStatusCode.InternalServerError
			},
			_ => new StatusCodeResult((int)HttpStatusCode.InternalServerError)
		};
	}
}
