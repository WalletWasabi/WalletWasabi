using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using Mono.Options;
using NBitcoin;


namespace WasabiPasswordFinder
{
    internal class Program
    {
        private static Dictionary<string, string> Charsets = new Dictionary<string, string>{
            ["en"] = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
            ["es"] = "aábcdeéfghiíjkmnñoópqrstuúüvwxyzAÁBCDEÉFGHIÍJKLMNNOÓPQRSTUÚÜVWXYZ",
            ["pt"] = "aáàâābcçdeéêfghiíjkmnoóôōpqrstuúvwxyzAÁÀÂĀBCÇDEÉÊFGHIÍJKMNOÓÔŌPQRSTUÚVWXYZ",
            ["it"] = "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ",
            ["fr"] = "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ",
        };

        private static void Main(string[] args)
        {
            var language   = "en";
            var useNumbers = true;
            var useSymbols = true;
            var secret     = string.Empty;
            var help       = false;

            var options = new OptionSet () {
                { "s|secret=", "The secret from your .json file (EncryptedSecret).",
                    v => secret = v },
                { "l|language=", "The charset to use: en, es, it, fr, pt. Default=en.",
                    v => language = v },
                { "n|numbers=", "Try passwords with numbers. Default=true.",
                    v => useNumbers = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
                { "x|symbols=", "Try passwords with symbolds. Default=true.",
                    v => useSymbols = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
                { "h|help", "Show Help",
                    v => help = true}};

            options.Parse(args);
            if (help || string.IsNullOrEmpty(secret) || !Charsets.ContainsKey(language))
            {
                ShowHelp(options);
                return;
            }

            BitcoinEncryptedSecretNoEC encryptedSecret;
            try
            {
                encryptedSecret = new BitcoinEncryptedSecretNoEC(secret);
            }
            catch(FormatException)
            {
                Console.WriteLine("ERROR: The encrypted secret is invalid. Make sure you copied correctly from your wallet file.");
                return;
            }

            Console.WriteLine($"WARNING: This tool will display you password if it finds it. Also, the process status display your wong password chars.");
            Console.WriteLine($"         You can cancel this by CTRL+C combination anytime." + Environment.NewLine);

            Console.Write("Enter password: ");

            var password = GetPasswords();
            var charset = Charsets[language] + (useNumbers ? "0123456789" : "") + (useSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

            var found = false;
            var lastpwd = string.Empty;
            var attempts = 0;
            var maxNumberAttempts = password.Length * charset.Length;
            var stepSize = (maxNumberAttempts + 101) / 100;


            Console.WriteLine();
            Console.Write($"[{string.Empty, 100}] 0%");

            var sw = new Stopwatch();
            sw.Start();
            foreach(var pwd in GeneratePasswords(password, charset.ToArray()))
            {
                lastpwd = pwd;
                try
                {
                    encryptedSecret.GetKey(pwd);
                    found = true; 
                    break;
                }
                catch (SecurityException)
                {
                }
                Progress(++attempts, stepSize, maxNumberAttempts, sw.Elapsed);
            }
            sw.Stop();

            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"Completed in {sw.Elapsed}");
            Console.WriteLine(found ? $"SUCCESS: Password found: >>> {lastpwd} <<<" : "FAILED: Password not found");
            Console.WriteLine();
        }

        private static string GetPasswords()
        {
            var stack = new Stack<char>();
            var nextKey = Console.ReadKey(true);

            while (nextKey.Key != ConsoleKey.Enter)
            {
                if (nextKey.Key == ConsoleKey.Backspace)
                {
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    stack.Push(nextKey.KeyChar);
                    Console.Write("*");
                }
                nextKey = Console.ReadKey(true);
            }
            return new string(stack.Reverse().ToArray());
        }

        private static void Progress(int iter, int stepSize, int max, TimeSpan elapsed)
        {
            if(iter % stepSize == 0)
            {
                var percentage = (int)((float)iter / max * 100);
                var estimatedTime = elapsed / percentage * (100 - percentage);
                var bar = new string('#', percentage);

                Console.CursorLeft = 0;
                Console.Write($"[{bar, -100}] {percentage}% - ET: {estimatedTime}");
            }
        }

        private static void ShowHelp (OptionSet p)
        {
            Console.WriteLine ("Usage: dotnet run [OPTIONS]+");
            Console.WriteLine ("Example: dotnet run -s=\"6PYSeErf23ArQL7xXUWPKa3VBin6cuDaieSdABvVyTA51dS4Mxrtg1CpGN\" -p=\"password\"");
            Console.WriteLine ();
            Console.WriteLine ("Options:");
            p.WriteOptionDescriptions (Console.Out);
        }

        private static IEnumerable<string> GeneratePasswords(string password, char[] charset)
        {
            var pwChar = password.ToCharArray();
            for(var i=0; i < pwChar.Length; i++)
            {
                var original = pwChar[i]; 
                foreach(var c in charset)
                {
                    pwChar[i] = c;
                    yield return new string(pwChar); 
                }
                pwChar[i] = original; 
            }
        }
    }
}
