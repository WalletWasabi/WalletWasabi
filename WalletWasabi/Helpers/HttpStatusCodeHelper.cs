namespace System.Net
{
	public static class HttpStatusCodeHelper
	{
		/// <summary>
		/// 1xx
		/// </summary>
		public static bool IsInformational(HttpStatusCode status)
		{
			return ((int)status).ToString()[0] == '1';
		}

		/// <summary>
		/// 2xx
		/// </summary>
		public static bool IsSuccessful(HttpStatusCode status)
		{
			return ((int)status).ToString()[0] == '2';
		}

		public static bool IsValidCode(int codeToValidate)
		{
			foreach (var code in Enum.GetValues(typeof(HttpStatusCode)))
			{
				if ((int)code == codeToValidate)
				{
					return true;
				}
			}

			return false;
		}
	}
}
