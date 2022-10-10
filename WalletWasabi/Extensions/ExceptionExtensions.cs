using System.Collections.Generic;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Models;

namespace WalletWasabi.Extensions;

public static class ExceptionExtensions
{
	public static string ToTypeMessageString(this Exception ex)
	{
		var trimmed = Guard.Correct(ex.Message);

		if (trimmed.Length == 0)
		{
			if (ex is HwiException hwiEx)
			{
				return $"{hwiEx.GetType().Name}: {hwiEx.ErrorCode}";
			}
			return ex.GetType().Name;
		}
		else
		{
			return $"{ex.GetType().Name}: {ex.Message}";
		}
	}

	public static string ToUserFriendlyString(this Exception ex)
	{
		var trimmed = Guard.Correct(ex.Message);
		if (trimmed.Length == 0)
		{
			return ex.ToTypeMessageString();
		}
		else
		{
			if (ex is HwiException hwiEx)
			{
				if (hwiEx.ErrorCode == HwiErrorCode.DeviceConnError)
				{
					return "Could not find the hardware wallet.\nMake sure it is connected.";
				}
				else if (hwiEx.ErrorCode == HwiErrorCode.ActionCanceled)
				{
					return "The transaction was canceled on the device.";
				}
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
	}

	public static SerializableException ToSerializableException(this Exception ex)
	{
		return new SerializableException(ex);
	}
}
