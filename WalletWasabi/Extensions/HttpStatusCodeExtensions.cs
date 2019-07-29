using System.Net.Http;

namespace System.Net
{
	public static class HttpStatusCodeExtensions
	{
		public static string ToReasonString(this HttpStatusCode me)
		{
			using (var message = new HttpResponseMessage(me))
			{
				return message.ReasonPhrase;
			}
		}
	}
}
