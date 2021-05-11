using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace WalletWasabi.Backend.Filters
{
	public class IdempotentActionFilterAttributeImpl : ActionFilterAttribute
	{
		private readonly IMemoryCache _cache;

		public IdempotentActionFilterAttributeImpl(IMemoryCache cache)
			: base()
		{
			_cache = cache;
		}


		public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var request = context.HttpContext.Request;

			var (cacheKey, body) = await GetCacheEntryKeyAsync(request);

			if (_cache.TryGetValue<ObjectResult>(cacheKey, out var cachedResponse))
			{
				context.Result = cachedResponse;
				context.HttpContext.Items.Remove("cached-key");
				return;
			}

			context.HttpContext.Items["cached-key"] = cacheKey;

 			await next();
		}

		public override void OnResultExecuted(ResultExecutedContext context)
		{
			if (context.HttpContext.Items.TryGetValue("cached-key", out var cacheKey) && cacheKey is not null)
			{
				_cache.Set(cacheKey, context.Result, DateTimeOffset.UtcNow.AddMinutes(5));
			}
		}

		private async Task<(string, string)> GetCacheEntryKeyAsync(HttpRequest request)
		{
			using var reader = new System.IO.StreamReader(request.Body);
			var body = await reader.ReadToEndAsync();

			var rawKey = string.Join(":", request.Path, body);
			using var sha256Hash = SHA256.Create();
			var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawKey));

			return ("arena-request-cache-key: " + ByteHelpers.ToHex(bytes), body);
		}
	}
}
