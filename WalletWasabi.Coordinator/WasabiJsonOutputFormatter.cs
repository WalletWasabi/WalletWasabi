using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using WalletWasabi.Logging;

namespace WalletWasabi.Coordinator;

public delegate JsonNode JsonEncoder(object obj);

public class WasabiJsonOutputFormatter : TextOutputFormatter
{
	private readonly JsonEncoder _encoder;

	public WasabiJsonOutputFormatter(JsonEncoder encoder)
	{
		_encoder = encoder;
		SupportedEncodings.Add(Encoding.UTF8);

		SupportedMediaTypes.Add("application/json");
		SupportedMediaTypes.Add("text/json");
		SupportedMediaTypes.Add("application/*+json");
	}

	protected override bool CanWriteType(Type? type) => type != typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails);

	public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
	{
		HttpContext httpContext = context.HttpContext;
		Stream responseStream = httpContext.Response.Body;
		try
		{
			await using Utf8JsonWriter writter = new(responseStream);
			_encoder(context.Object).WriteTo(writter);
			await responseStream.FlushAsync(httpContext.RequestAborted);
		}
		catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
		{
		}
		catch(Exception e)
		{
			Logger.LogError(e);
		}
	}
}
