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
		private static TimeSpan CacheTimeout { get; } = TimeSpan.FromMinutes(5);
		private const string ContextCacheKey = "cached-key";
		private readonly IMemoryCache _responseCache;

		public IdempotentActionFilterAttributeImpl(IMemoryCache cache)
			: base()
		{
			_responseCache = cache;
		}

		/// <inheritdoc/>
		public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			HttpRequest request = context.HttpContext.Request;

			if (context.ModelState.IsValid)
			{
				if (context.ActionArguments.TryGetValue("request", out object? model))
				{
					string cacheKey = GetCacheEntryKey(request.Path, model);

					if (_responseCache.TryGetValue(cacheKey, out ObjectResult? cachedResponse))
					{
						context.Result = cachedResponse;
						context.HttpContext.Items.Remove(ContextCacheKey);
						return;
					}

					context.HttpContext.Items[ContextCacheKey] = cacheKey;
				}
				else
				{
					throw new InvalidOperationException("Control actions marked as Idempotent must receive a 'request' argument.");
				}
			}

			await next().ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public override void OnResultExecuted(ResultExecutedContext context)
		{
			if (context.HttpContext.Items.TryGetValue(ContextCacheKey, out object? cacheKey) && cacheKey is not null)
			{
				_responseCache.Set(cacheKey, context.Result, DateTimeOffset.UtcNow.Add(CacheTimeout));
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
