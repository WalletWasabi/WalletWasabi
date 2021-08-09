using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Backend.Filters
{
	public class IdempotentActionFilterAttributeImpl : ActionFilterAttribute
	{
		private static TimeSpan CacheTimeout { get; } = TimeSpan.FromMinutes(5);
		private static TimeSpan RequestTimeout { get; } = TimeSpan.FromMinutes(1);
		private const string ContextCacheKey = "cached-key";

		public IdempotentActionFilterAttributeImpl(IMemoryCache cache)
			: base()
		{
			ResponseCache = cache;
		}

		/// <summary>Guards <see cref="ResponseCache"/>.</summary>
		/// <remarks>Unfortunately, <see cref="CacheExtensions.GetOrCreate{TItem}(IMemoryCache, object, Func{ICacheEntry, TItem})"/> is not atomic.</remarks>
		/// <seealso href="https://github.com/dotnet/runtime/issues/36499"/>
		private object ResponseCacheLock { get; } = new();

		/// <remarks>Guarded by <see cref="ResponseCacheLock"/>.</remarks>
		private IMemoryCache ResponseCache { get; }

		/// <inheritdoc/>
		public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			HttpRequest request = context.HttpContext.Request;

			if (context.ModelState.IsValid)
			{
				if (!context.ActionArguments.TryGetValue("request", out object? model))
				{
					throw new InvalidOperationException("Control actions marked as Idempotent must receive a 'request' argument.");
				}

				string cacheKey = GetCacheEntryKey(request.Path, model);
				TaskCompletionSource<IActionResult>? cachedResponseTcs = null;
				TaskCompletionSource<IActionResult>? responseTcs = null;

				lock (ResponseCacheLock)
				{
					if (!ResponseCache.TryGetValue(cacheKey, out cachedResponseTcs))
					{
						responseTcs = new();
						ResponseCache.Set(cacheKey, responseTcs, DateTimeOffset.UtcNow.Add(CacheTimeout));
					}
				}

				if (cachedResponseTcs is not null)
				{
					try
					{
						context.Result = await cachedResponseTcs!.Task.WithAwaitCancellationAsync(RequestTimeout).ConfigureAwait(false);
						context.HttpContext.Items.Remove(ContextCacheKey);
						return;
					}
					catch (OperationCanceledException)
					{
						// Failed to get cached response. Continue as if it were a non-cached request.
					}
				}

				context.HttpContext.Items[ContextCacheKey] = cacheKey;

				ActionExecutedContext executed = await next().ConfigureAwait(false);

				// Request failed for some reason, we don't want to hold any other requests that wait for this one request to finish.
				if (executed.Exception != null && !executed.ExceptionHandled)
				{
					responseTcs?.TrySetCanceled();
				}
			}
			else
			{
				await next().ConfigureAwait(false);
			}
		}

		/// <inheritdoc/>
		public override void OnResultExecuted(ResultExecutedContext context)
		{
			if (context.HttpContext.Items.TryGetValue(ContextCacheKey, out object? cacheKey) && cacheKey is not null)
			{
				lock (ResponseCacheLock)
				{
					if (ResponseCache.TryGetValue(cacheKey, out TaskCompletionSource<IActionResult>? cachedResponseTcs))
					{
						cachedResponseTcs!.TrySetResult(context.Result);
					}
				}
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
