using Microsoft.AspNetCore.Mvc;

namespace WalletWasabi.Backend.Filters
{
	public class IdempotentAttribute : TypeFilterAttribute
	{
		public IdempotentAttribute()
			: base(typeof(IdempotentActionFilterAttributeImpl))
		{
		}
	}
}
