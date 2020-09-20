using System;
using System.Text;

namespace WalletWasabi.Userfacing
{
	public static class PasswordConsole
	{
		/// <summary>
		/// Gets the console password.
		/// </summary>
		public static string ReadPassword()
		{
			try
			{
				var sb = new StringBuilder();
				while (true)
				{
					ConsoleKeyInfo cki = Console.ReadKey(true);
					if (cki.Key == ConsoleKey.Enter)
					{
						Console.WriteLine();
						break;
					}

					if (cki.Key == ConsoleKey.Backspace)
					{
						if (sb.Length > 0)
						{
							Console.Write("\b \b");
							sb.Length--;
						}

						continue;
					}

					Console.Write('*');
					sb.Append(cki.KeyChar);
				}

				return sb.ToString();
			}
			catch (InvalidOperationException)
			{
				return Console.ReadLine();
			}
		}
	}
}
