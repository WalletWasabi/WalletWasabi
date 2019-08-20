using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static WalletWasabi.Http.Constants;

namespace WalletWasabi.Http.Models
{
	public abstract class StartLine
	{
		public HttpProtocol Protocol { get; protected set; }
		public string StartLineString { get; protected set; }

		public override string ToString()
		{
			return StartLineString;
		}

		public static async Task<IEnumerable<string>> GetPartsAsync(string startLineString)
		{
			var trimmed = "";
			// check if an unexpected crlf in the startlinestring
			using (var reader = new StringReader(startLineString))
			{
				// read to CRLF, if it does not end with that it reads to end, if it does, it removes it
				trimmed = reader.ReadLine(strictCRLF: true);
				// startLineString must end here
				if (reader.Read() != -1)
				{
					throw new Exception($"Wrong {startLineString} provided.");
				}
			}

			var parts = new List<string>();
			using (var reader = new StringReader(trimmed))
			{
				while (true)
				{
					var part = reader.ReadPart(SP.ToCharArray()[0]);

					if (part is null || part == "")
					{
						break;
					}

					if (parts.Count == 2)
					{
						var rest = await reader.ReadToEndAsync();

						// startLineString must end here, the ReadToEnd returns "" if nothing to read instead of null
						if (rest != "")
						{
							part += SP + rest;
						}
					}
					parts.Add(part);
				}
			}
			return parts;
		}
	}
}
