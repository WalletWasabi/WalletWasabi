using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Hwi2.Exceptions
{
	public enum HwiErrorCode
	{
		NoDevicePath = -1,
		NoDeviceType = -2,
		DeviceConnError = -3,
		UnknownDeviceType = -4,
		InvalidTx = -5,
		NoPassword = -6,
		BadArgument = -7,
		NotImplemented = -8,
		UnavailableAction = -9,
		DeviceAlreadyInit = -10,
		DeviceAlreadyUnlocked = -11,
		DeviceNotReady = -12,
		UnknownError = -13,
		ActionCanceled = -14,
		DeviceBusy = -15,
		NeedToBeRoot = -16,
		DeviceNotInitialized = -18
	}
}
