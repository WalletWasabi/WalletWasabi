using System.Collections.Generic;
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
			return Lang.Resources.Exception_Generic_Friendly;
		}

		if (TryFindRpcErrorMessage(exceptionMessage, out var friendlyMessage))
		{
			return friendlyMessage;
		}

		return ex switch
		{
			HwiException hwiEx => GetFriendlyHwiExceptionMessage(hwiEx),
			HttpRequestException => Lang.Resources.Exception_HttpRequest_Friendly,
			UnauthorizedAccessException => Lang.Resources.Exception_UnauthorizedAccess_Friendly,
			_ => ex.Message
		};
	}

	private static string GetFriendlyHwiExceptionMessage(HwiException hwiEx)
	{
		return hwiEx.ErrorCode switch
		{
			HwiErrorCode.DeviceConnError => Lang.Resources.HWI_Error_DeviceConn,
			HwiErrorCode.ActionCanceled => Lang.Resources.HWI_Error_ActionCanceled,
			HwiErrorCode.UnknownError => Lang.Resources.HWI_Error_Unknown,
			_ => hwiEx.Message
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
