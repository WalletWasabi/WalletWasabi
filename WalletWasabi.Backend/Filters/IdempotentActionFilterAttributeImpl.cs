using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi.Models.Serialization;

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
			HttpRequest request = context.HttpContext.Request;

			if (context.ModelState.IsValid)
			{
				if (context.ActionArguments.TryGetValue("request", out object? model))
				{
					string cacheKey = GetCacheEntryKey(request.Path, model);

					if (_cache.TryGetValue(cacheKey, out ObjectResult? cachedResponse))
					{
						context.Result = cachedResponse;
						context.HttpContext.Items.Remove("cached-key");
						return;
					}

					context.HttpContext.Items["cached-key"] = cacheKey;
				}
				else
				{
					throw new InvalidOperationException("Control actions marked as Idempotent must receive a 'request' argument.");
				}
			}

			await next().ConfigureAwait(false);
		}

		public override void OnResultExecuted(ResultExecutedContext context)
		{
			if (context.HttpContext.Items.TryGetValue("cached-key", out object? cacheKey) && cacheKey is not null)
			{
				_cache.Set(cacheKey, context.Result, DateTimeOffset.UtcNow.AddMinutes(5));
			}
		}

		private string GetCacheEntryKey(string path, object model)
		{
			string json = JsonConvert.SerializeObject(model, JsonSerializationOptions.Default.Settings);
			string rawKey = string.Join(":", path, json);
			using var sha256Hash = SHA256.Create();
			byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawKey));

			return "arena-request-cache-key: " + ByteHelpers.ToHex(bytes);
		}
	}
}
