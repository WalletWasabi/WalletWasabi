using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;

namespace WalletWasabi.Fluent.Extensions;

public static class FriendlyExceptionMessageExtensions
{
	public static string ToUserFriendlyString(this Exception ex)
	{
		var trimmed = Guard.Correct(ex.Message);
		if (trimmed.Length == 0)
		{
			return ex.ToTypeMessageString();
		}

		switch (ex)
		{
			case OperationCanceledException operationCanceledEx:
				return operationCanceledEx.Message;
			case HwiException hwiEx:
				return GetFriendlyHwiExceptionMessage(hwiEx);
			case HttpRequestException httpEx:
				return GetFriendlyHttpRequestExceptionMessage(httpEx);
		}

		foreach (KeyValuePair<string, string> pair in RpcErrorTools.ErrorTranslations)
		{
			if (trimmed.Contains(pair.Key, StringComparison.InvariantCultureIgnoreCase))
			{
				return pair.Value;
			}
		}

		return ex.ToTypeMessageString();
	}

	private static string GetFriendlyHwiExceptionMessage(HwiException hwiEx)
	{
		return hwiEx.ErrorCode switch
		{
			HwiErrorCode.DeviceConnError => "Could not find the hardware wallet.\nMake sure it is connected.",
			HwiErrorCode.ActionCanceled => "The transaction was canceled on the device.",
			HwiErrorCode.UnknownError => "Unknown error.\nMake sure the device is connected and isn't busy, then try again.",
			_ => hwiEx.Message
		};
	}

	private static string GetFriendlyHttpRequestExceptionMessage(HttpRequestException httpEx)
	{
		return httpEx.StatusCode switch
		{	
			HttpStatusCode.BadRequest => "An unexpected network error occured. Try again.",
			_ => httpEx.Message
		};
	}
}
