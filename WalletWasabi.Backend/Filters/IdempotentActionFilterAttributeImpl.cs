using System;
using System.Security.Cryptography;
using System.Text;
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

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var request = context.HttpContext.Request;

			var (cacheKey, body) = GetCacheEntryKey(request);

			if (_cache.TryGetValue<OkObjectResult>(cacheKey, out var cachedResponse))
			{
				context.Result = cachedResponse;
				return;
			}

			context.HttpContext.Items["cached-key"] = cacheKey;
		}

		public override void OnResultExecuted(ResultExecutedContext context)
		{
			var cacheKey = context.HttpContext.Items["cached-key"];

			_cache.Set(cacheKey, context.Result, DateTimeOffset.UtcNow.AddMinutes(5));
		}

		private (string, string) GetCacheEntryKey(HttpRequest request)
		{
			using var reader = new System.IO.StreamReader(request.Body);
			var body = reader.ReadToEnd();

			var rawKey = string.Join(":", request.Path, body);
			using var sha256Hash = SHA256.Create();
			var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawKey));

			return ("arena-request-cache-key: " + ByteHelpers.ToHex(bytes), body);
		}
	}
}
