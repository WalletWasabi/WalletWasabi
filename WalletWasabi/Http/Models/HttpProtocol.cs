using System;

// https://tools.ietf.org/html/rfc7230#section-2.6
namespace WalletWasabi.Http.Models
{
	public class HttpProtocol : IEquatable<HttpProtocol>, IEquatable<string>
	{
		public Version Version { get; }
		public const string Protocol = "HTTP";
		public static HttpProtocol HTTP11 = new HttpProtocol("HTTP/1.1");
		public static HttpProtocol HTTP10 = new HttpProtocol("HTTP/1.0");

		public HttpProtocol(string protocolString)
		{
			try
			{
				var parts = protocolString.Trim().Split(new char[] { '/' });
				if (parts.Length != 2)
				{
					throw new FormatException($"Wrong {nameof(HttpProtocol)} format: {protocolString}.");
				}

				if (parts[1].Split(new char[] { '.' }).Length != 2)
				{
					throw new FormatException($"Wrong {nameof(HttpProtocol)} format: {protocolString}.");
				}

				Version = new Version(parts[1]);

				string protocol = GetProtocol(protocolString);
				if (protocol != Protocol)
				{
					throw new NotSupportedException($"Wrong protocol {nameof(HttpProtocol)}: {protocolString}.");
				}
			}
			catch (Exception ex)
			{
				throw new FormatException($"Wrong {nameof(HttpProtocol)} format: {protocolString}.", ex);
			}
		}

		private static string GetProtocol(string protocolString)
		{
			return protocolString.Trim().Split(new char[] { '/' })[0];
		}

		// HTTP-name "/" DIGIT "." DIGIT
		public override string ToString()
		{
			return $"{Protocol}/{Version}";
		}

		#region Equality

		public override bool Equals(object obj)
		{
			return obj is HttpProtocol protocol && this == protocol;
		}

		public bool Equals(HttpProtocol other)
		{
			return this == other;
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public static bool operator ==(HttpProtocol x, HttpProtocol y) => x?.ToString() == y?.ToString();

		public static bool operator !=(HttpProtocol x, HttpProtocol y) => !(x == y);

		public bool Equals(string other)
		{
			return ToString() == other;
		}

		public static bool operator ==(string x, HttpProtocol y) => x == y?.ToString();

		public static bool operator ==(HttpProtocol x, string y) => x?.ToString() == y;

		public static bool operator !=(string x, HttpProtocol y) => !(x == y);

		public static bool operator !=(HttpProtocol x, string y) => !(x == y);

		#endregion Equality
	}
}
