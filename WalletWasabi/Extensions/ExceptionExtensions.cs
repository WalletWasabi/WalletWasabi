namespace System
{
	public static class ExceptionExtensions
	{
		public static string ToTypeMessageString(this Exception ex)
		{
			return $"{ex.GetType().Name}: {ex.Message}";
		}
	}
}
