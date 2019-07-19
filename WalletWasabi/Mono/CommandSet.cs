//
// Options.cs
//
// Authors:
//  Jonathan Pryor <jpryor@novell.com>, <Jonathan.Pryor@microsoft.com>
//  Federico Di Gregorio <fog@initd.org>
//  Rolf Bjarne Kvinge <rolf@xamarin.com>
//
// Copyright (C) 2008 Novell (http://www.novell.com)
// Copyright (C) 2009 Federico Di Gregorio.
// Copyright (C) 2012 Xamarin Inc (http://www.xamarin.com)
// Copyright (C) 2017 Microsoft Corporation (http://www.microsoft.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Compile With:
//   mcs -debug+ -r:System.Core Options.cs -o:Mono.Options.dll -t:library
//   mcs -debug+ -d:LINQ -r:System.Core Options.cs -o:Mono.Options.dll -t:library
//
// The LINQ version just changes the implementation of
// OptionSet.Parse(IEnumerable<string>), and confers no semantic changes.

//
// A Getopt::Long-inspired option parsing library for C#.
//
// Mono.Options.OptionSet is built upon a key/value table, where the
// key is a option format string and the value is a delegate that is
// invoked when the format string is matched.
//
// Option format strings:
//  Regex-like BNF Grammar:
//    name: .+
//    type: [=:]
//    sep: ( [^{}]+ | '{' .+ '}' )?
//    aliases: ( name type sep ) ( '|' name type sep )*
//
// Each '|'-delimited name is an alias for the associated action.  If the
// format string ends in a '=', it has a required value.  If the format
// string ends in a ':', it has an optional value.  If neither '=' or ':'
// is present, no value is supported.  `=' or `:' need only be defined on one
// alias, but if they are provided on more than one they must be consistent.
//
// Each alias portion may also end with a "key/value separator", which is used
// to split option values if the option accepts > 1 value.  If not specified,
// it defaults to '=' and ':'.  If specified, it can be any character except
// '{' and '}' OR the *string* between '{' and '}'.  If no separator should be
// used (i.e. the separate values should be distinct arguments), then "{}"
// should be used as the separator.
//
// Options are extracted either from the current option by looking for
// the option name followed by an '=' or ':', or is taken from the
// following option IFF:
//  - The current option does not contain a '=' or a ':'
//  - The current option requires a value (i.e. not a Option type of ':')
//
// The `name' used in the option format string does NOT include any leading
// option indicator, such as '-', '--', or '/'.  All three of these are
// permitted/required on any named option.
//
// Option bundling is permitted so long as:
//   - '-' is used to start the option group
//   - all of the bundled options are a single character
//   - at most one of the bundled options accepts a value, and the value
//     provided starts from the next character to the end of the string.
//
// This allows specifying '-a -b -c' as '-abc', and specifying '-D name=value'
// as '-Dname=value'.
//
// Option processing is disabled by specifying "--".  All options after "--"
// are returned by OptionSet.Parse() unchanged and unprocessed.
//
// Unprocessed options are returned from OptionSet.Parse().
//
// Examples:
//  int verbose = 0;
//  OptionSet p = new OptionSet ()
//    .Add ("v", v => ++verbose)
//    .Add ("name=|value=", v => Console.WriteLine (v));
//  p.Parse (new string[]{"-v", "--v", "/v", "-name=A", "/name", "B", "extra"});
//
// The above would parse the argument string array, and would invoke the
// lambda expression three times, setting `verbose' to 3 when complete.
// It would also print out "A" and "B" to standard output.
// The returned array would contain the string "extra".
//
// C# 3.0 collection initializers are supported and encouraged:
//  var p = new OptionSet () {
//    { "h|?|help", v => ShowHelp () },
//  };
//
// System.ComponentModel.TypeConverter is also supported, allowing the use of
// custom data types in the callback type; TypeConverter.ConvertFromString()
// is used to convert the value option to an instance of the specified
// type:
//
//  var p = new OptionSet () {
//    { "foo=", (Foo f) => Console.WriteLine (f.ToString ()) },
//  };
//
// Random other tidbits:
//  - Boolean options (those w/o '=' or ':' in the option format string)
//    are explicitly enabled if they are followed with '+', and explicitly
//    disabled if they are followed with '-':
//      string a = null;
//      var p = new OptionSet () {
//        { "a", s => a = s },
//      };
//      p.Parse (new string[]{"-a"});   // sets v != null
//      p.Parse (new string[]{"-a+"});  // sets v != null
//      p.Parse (new string[]{"-a-"});  // sets v is null
//

//
// Mono.Options.CommandSet allows easily having separate commands and
// associated command options, allowing creation of a *suite* along the
// lines of **git**(1), **svn**(1), etc.
//
// CommandSet allows intermixing plain text strings for `--help` output,
// Option values -- as supported by OptionSet -- and Command instances,
// which have a name, optional help text, and an optional OptionSet.
//
//  var suite = new CommandSet ("suite-name") {
//    // Use strings and option values, as with OptionSet
//    "usage: suite-name COMMAND [OPTIONS]+",
//    { "v:", "verbosity", (int? v) => Verbosity = v.HasValue ? v.Value : Verbosity+1 },
//    // Commands may also be specified
//    new Command ("command-name", "command help") {
//      Options = new OptionSet {/*...*/},
//      Run     = args => { /*...*/},
//    },
//    new MyCommandSubclass (),
//  };
//  return suite.Run (new string[]{...});
//
// CommandSet provides a `help` command, and forwards `help COMMAND`
// to the registered Command instance by invoking Command.Invoke()
// with `--help` as an option.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessageLocalizerConverter = System.Converter<string, string>;

namespace Mono.Options
{
	public class CommandSet : KeyedCollection<string, Command>
	{
		public OptionSet Options { get; private set; }

		public CommandSet(string suite, MessageLocalizerConverter localizer = null)
			: this(suite, Console.Out, Console.Error, localizer)
		{
		}

		public CommandSet(string suite, TextWriter output, TextWriter error, MessageLocalizerConverter localizer = null)
		{
			Suite = suite ?? throw new ArgumentNullException(nameof(suite));
			Options = new CommandOptionSet(this, localizer);
			Out = output ?? throw new ArgumentNullException(nameof(output));
			Error = error ?? throw new ArgumentNullException(nameof(error));
		}

		public string Suite { get; }
		public TextWriter Out { get; private set; }
		public TextWriter Error { get; private set; }
		public MessageLocalizerConverter MessageLocalizer => Options.MessageLocalizer;

		public List<CommandSet> NestedCommandSets { get; set; }
		public HelpCommand Help { get; set; }
		public bool ShowHelp { get; set; }

		protected override string GetKeyForItem(Command item)
		{
			return item?.Name;
		}

		public new CommandSet Add(Command value)
		{
			if (value is null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			AddCommand(value);
			Options.Add(new CommandOption(value));
			return this;
		}

		private void AddCommand(Command value)
		{
			if (value.CommandSet != null && value.CommandSet != this)
			{
				throw new ArgumentException($"Command instances can only be added to a single {nameof(CommandSet)}.", nameof(value));
			}
			value.CommandSet = this;
			if (value.Options != null)
			{
				value.Options.MessageLocalizer = Options.MessageLocalizer;
			}

			base.Add(value);

			Help = Help ?? value as HelpCommand;
		}

		public CommandSet Add(string header)
		{
			Options.Add(header);
			return this;
		}

		public CommandSet Add(Option option)
		{
			Options.Add(option);
			return this;
		}

		public CommandSet Add(string prototype, Action<string> action)
		{
			Options.Add(prototype, action);
			return this;
		}

		public CommandSet Add(string prototype, string description, Action<string> action)
		{
			Options.Add(prototype, description, action);
			return this;
		}

		public CommandSet Add(string prototype, string description, Action<string> action, bool hidden)
		{
			Options.Add(prototype, description, action, hidden);
			return this;
		}

		public CommandSet Add(string prototype, OptionAction<string, string> action)
		{
			Options.Add(prototype, action);
			return this;
		}

		public CommandSet Add(string prototype, string description, OptionAction<string, string> action)
		{
			Options.Add(prototype, description, action);
			return this;
		}

		public CommandSet Add(string prototype, string description, OptionAction<string, string> action, bool hidden)
		{
			Options.Add(prototype, description, action, hidden);
			return this;
		}

		public CommandSet Add<T>(string prototype, Action<T> action)
		{
			Options.Add(prototype, null, action);
			return this;
		}

		public CommandSet Add<T>(string prototype, string description, Action<T> action)
		{
			Options.Add(prototype, description, action);
			return this;
		}

		public CommandSet Add<TKey, TValue>(string prototype, OptionAction<TKey, TValue> action)
		{
			Options.Add(prototype, action);
			return this;
		}

		public CommandSet Add<TKey, TValue>(string prototype, string description, OptionAction<TKey, TValue> action)
		{
			Options.Add(prototype, description, action);
			return this;
		}

		public CommandSet Add(ArgumentSource source)
		{
			Options.Add(source);
			return this;
		}

		public CommandSet Add(CommandSet nestedCommands)
		{
			if (nestedCommands is null)
			{
				throw new ArgumentNullException(nameof(nestedCommands));
			}

			if (NestedCommandSets is null)
			{
				NestedCommandSets = new List<CommandSet>();
			}

			if (!AlreadyAdded(nestedCommands))
			{
				NestedCommandSets.Add(nestedCommands);
				foreach (var o in nestedCommands.Options)
				{
					if (o is CommandOption c)
					{
						Options.Add(new CommandOption(c.Command, $"{nestedCommands.Suite} {c.CommandName}"));
					}
					else
					{
						Options.Add(o);
					}
				}
			}

			nestedCommands.Options = Options;
			nestedCommands.Out = Out;
			nestedCommands.Error = Error;

			return this;
		}

		private bool AlreadyAdded(CommandSet value)
		{
			if (value == this)
			{
				return true;
			}

			if (NestedCommandSets is null)
			{
				return false;
			}

			foreach (var nc in NestedCommandSets)
			{
				if (nc.AlreadyAdded(value))
				{
					return true;
				}
			}
			return false;
		}

		public IEnumerable<string> GetCompletions(string prefix = null)
		{
			ExtractToken(ref prefix, out string rest);

			foreach (var command in this)
			{
				if (command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					yield return command.Name;
				}
			}

			if (NestedCommandSets is null)
			{
				yield break;
			}

			foreach (var subset in NestedCommandSets)
			{
				if (subset.Suite.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					foreach (var c in subset.GetCompletions(rest))
					{
						yield return $"{subset.Suite} {c}";
					}
				}
			}
		}

		private static void ExtractToken(ref string input, out string rest)
		{
			rest = "";
			input = input ?? "";

			int top = input.Length;
			for (int i = 0; i < top; i++)
			{
				if (char.IsWhiteSpace(input[i]))
				{
					continue;
				}

				for (int j = i + 1; j < top; j++)
				{
					if (char.IsWhiteSpace(input[j]))
					{
						rest = input.Substring(j).Trim();
						input = input.Substring(i, j).Trim();
						return;
					}
				}
				rest = "";
				if (i != 0)
				{
					input = input.Substring(i).Trim();
				}

				return;
			}
		}

		public async Task<int> RunAsync(IEnumerable<string> arguments)
		{
			if (arguments is null)
			{
				throw new ArgumentNullException(nameof(arguments));
			}

			ShowHelp = false;
			if (Help is null)
			{
				Help = new HelpCommand();
				AddCommand(Help);
			}
			if (!Options.Contains("help"))
			{
				Options.Add("help", "", v => ShowHelp = v != null, hidden: true);
			}
			if (!Options.Contains("?"))
			{
				Options.Add("?", "", v => ShowHelp = v != null, hidden: true);
			}
			var extra = Options.Parse(arguments);
			if (extra.Count == 0)
			{
				if (ShowHelp)
				{
					return await Help.InvokeAsync(extra);
				}
				if (arguments.All(x => !x.Contains("version")))
				{
					Out.WriteLine(Options.MessageLocalizer($"Use `{Suite} help` for usage."));
				}
				return 1;
			}
			var command = GetCommand(extra);
			if (command is null)
			{
				Help.WriteUnknownCommand(extra[0]);
				return 1;
			}
			if (ShowHelp)
			{
				if (command.Options?.Contains("help") ?? true)
				{
					extra.Add("--help");
					return await command.InvokeAsync(extra);
				}
				command.Options.WriteOptionDescriptions(Out);
				return 0;
			}
			return await command.InvokeAsync(extra);
		}

		public Command GetCommand(List<string> extra)
		{
			return TryGetLocalCommand(extra) ?? TryGetNestedCommand(extra);
		}

		private Command TryGetLocalCommand(List<string> extra)
		{
			var name = extra[0];
			if (Contains(name))
			{
				extra.RemoveAt(0);
				return this[name];
			}
			for (int i = 1; i < extra.Count; ++i)
			{
				name = name + " " + extra[i];
				if (!Contains(name))
				{
					continue;
				}

				extra.RemoveRange(0, i + 1);
				return this[name];
			}
			return null;
		}

		private Command TryGetNestedCommand(List<string> extra)
		{
			if (NestedCommandSets is null)
			{
				return null;
			}

			var nestedCommands = NestedCommandSets.Find(c => c.Suite == extra[0]);
			if (nestedCommands is null)
			{
				return null;
			}

			var extraCopy = new List<string>(extra);
			extraCopy.RemoveAt(0);
			if (extraCopy.Count == 0)
			{
				return null;
			}

			var command = nestedCommands.GetCommand(extraCopy);
			if (command != null)
			{
				extra.Clear();
				extra.AddRange(extraCopy);
				return command;
			}
			return null;
		}
	}
}
