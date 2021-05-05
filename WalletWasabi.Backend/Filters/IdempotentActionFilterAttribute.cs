using Microsoft.AspNetCore.Mvc;

namespace WalletWasabi.Backend.Filters
{
	public class IdempotentActionFilterAttribute : TypeFilterAttribute
	{
		public IdempotentActionFilterAttribute()
			: base(typeof(IdempotentActionFilterAttributeImpl))
		{
		}
	}
}
