using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WalletWasabi.WabiSabiClientLibrary.Filters;

public class ValidationFilterAttribute : Attribute, IAsyncActionFilter
{
	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		if (!context.ModelState.IsValid)
		{
			string messages = string.Join("; ", context.ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
			throw new Exception(messages);
		}

		await next();
	}
}
