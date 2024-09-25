using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Extensions;

public static class HttpResponseMessageExtensions
{
	public static async Task ThrowUnwrapExceptionFromContentAsync(this HttpResponseMessage me, CancellationToken cancellationToken)
	{
		try
		{
			await me.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (e.InnerException is { } innerException)
		{
			throw innerException;
		}
	}

	public static async Task ThrowRequestExceptionFromContentAsync(this HttpResponseMessage me, CancellationToken cancellationToken)
	{
		var errorMessage = "";

		if (me.Content is not null)
		{
			var contentString = await me.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var error = JsonConvert.DeserializeObject<Error>(
				contentString,
				new JsonSerializerSettings()
				{
					Converters = JsonSerializationOptions.Default.Settings.Converters,
					Error = (_, e) => e.ErrorContext.Handled = true // Try to deserialize an Error object
				});
			var innerException = error switch
			{
				{ Type: ProtocolConstants.ProtocolViolationType } => Enum.TryParse<WabiSabiProtocolErrorCode>(error.ErrorCode, out var code)
					? new WabiSabiProtocolException(code, error.Description, exceptionData: error.ExceptionData)
					: new NotSupportedException($"Received WabiSabi protocol exception with unknown '{error.ErrorCode}' error code.\n\tDescription: '{error.Description}'."),
				{ Type: "unknown" } => new Exception(error.Description),
				_ => null
			};

			if (innerException is not null)
			{
				throw new HttpRequestException("Remote coordinator responded with an error.", innerException, me.StatusCode);
			}

			// Remove " from beginning and end to ensure backwards compatibility and it's kind of trash, too.
			if (contentString.Count(f => f == '"') <= 2)
			{
				contentString = contentString.Trim('"');
			}

			if (!string.IsNullOrWhiteSpace(contentString))
			{
				errorMessage = $"\n{contentString}";
			}
		}

		throw new HttpRequestException($"{me.StatusCode.ToReasonString()}{errorMessage}");
	}
}
