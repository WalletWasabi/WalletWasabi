using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;

namespace WalletWasabi.Fluent.Extensions;

public static class FriendlyExceptionMessageExtensions
{
	public static string ToUserFriendlyString(this Exception ex)
	{
		var exceptionMessage = Guard.Correct(ex.Message);

		if (exceptionMessage.Length == 0)
		{
			return "An unexpected error occured. Please try again or contact support.";
		}

		if (TryFindRpcErrorMessage(exceptionMessage, out var friendlyMessage))
		{
			return friendlyMessage;
		}

		return ex switch
		{
			HwiException hwiEx => GetFriendlyHwiExceptionMessage(hwiEx),
			HttpRequestException httpEx => GetFriendlyHttpRequestExceptionMessage(httpEx),
			_ => ex.Message
		};
	}

	private static string GetFriendlyHwiExceptionMessage(HwiException hwiEx)
	{
		return hwiEx.ErrorCode switch
		{
			HwiErrorCode.DeviceConnError => "Could not find the hardware wallet. Make sure it is connected.",
			HwiErrorCode.ActionCanceled => "The transaction was canceled on the device.",
			HwiErrorCode.UnknownError => "Unknown error. Make sure the device is connected and isn't busy, then try again.",
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

	private static bool TryFindRpcErrorMessage(string exceptionMessage, out string friendlyMessage)
	{
		friendlyMessage = "";

		foreach (KeyValuePair<string, string> pair in RpcErrorTools.ErrorTranslations)
		{
			if (exceptionMessage.Contains(pair.Key, StringComparison.InvariantCultureIgnoreCase))
			{
				{
					friendlyMessage = pair.Value;
					return true;
				}
			}
		}

		return false;
	}
}
