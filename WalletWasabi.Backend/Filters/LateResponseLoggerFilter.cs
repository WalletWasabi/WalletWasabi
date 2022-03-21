using Microsoft.AspNetCore.Mvc.Filters;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.Backend.Filters;

public class LateResponseLoggerFilter : ExceptionFilterAttribute
{
	public override void OnException(ExceptionContext context)
	{
		if (context.Exception is not WrongPhaseException ex)
		{
			return;
		}

		var actionName = ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor).ActionName;

		Logger.LogInfo($"Request '{actionName}' missing the phase '{string.Join(",", ex.ExpectedPhases)}' by '{ex.Late}'. Round id '{ex.RoundId}' is in phase '{ex.CurrentPhase}'.");
	}
}
