using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public static class HttpExtensions
    {
		public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage me)
		{
			var newMessage = new HttpRequestMessage(me.Method, me.RequestUri)
			{
				Version = me.Version
			};

			foreach (var header in me.Headers)
			{
				newMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			if (me.Content == null)
			{
				return newMessage;
			}

			var ms = new MemoryStream();
			await me.Content.CopyToAsync(ms);
			ms.Position = 0;
			var newContent = new StreamContent(ms);

			foreach (var header in me.Content.Headers)
			{
				newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			newMessage.Content = newContent;

			return newMessage;
		}
	}
}
