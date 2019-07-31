namespace WalletWasabi.Logging
{
	public enum LogMode
	{
		/// <summary>It uses Console.Write.</summary>
		Console,

		/// <summary>It uses Debug.Write.</summary>
		Debug,

		/// <summary>Logs into Log.txt, if filename is not specified.</summary>
		File
	}
}
