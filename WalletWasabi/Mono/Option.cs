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
using System.ComponentModel;
using System.Text;

namespace Mono.Options
{
	public abstract class Option
	{
		protected Option(string prototype, string description)
			: this(prototype, description, 1, false)
		{
		}

		protected Option(string prototype, string description, int maxValueCount)
			: this(prototype, description, maxValueCount, false)
		{
		}

		protected Option(string prototype, string description, int maxValueCount, bool hidden)
		{
			if (prototype is null)
			{
				throw new ArgumentNullException(nameof(prototype));
			}

			if (prototype == "")
			{
				throw new ArgumentException("Cannot be an empty string.", nameof(prototype));
			}

			if (maxValueCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxValueCount));
			}

			Prototype = prototype;
			Description = description;
			MaxValueCount = maxValueCount;
			Names = (this is OptionSet.Category)
				// append GetHashCode() so that "duplicate" categories have distinct
				// names, e.g. adding multiple "" categories should be valid.
				? new[] { prototype + GetHashCode() }
				: prototype.Split('|');

			if (this is OptionSet.Category || this is CommandOption)
			{
				return;
			}

			OptionValueType = ParsePrototype();
			Hidden = hidden;

			if (MaxValueCount == 0 && OptionValueType != OptionValueType.None)
			{
				throw new ArgumentException(
					$"Cannot provide {nameof(maxValueCount)} of 0 for {nameof(OptionValueType)}.{nameof(OptionValueType.Required)}" +
						$" or {nameof(OptionValueType)}.{nameof(OptionValueType.Optional)}.",
					nameof(maxValueCount));
			}

			if (OptionValueType == OptionValueType.None && maxValueCount > 1)
			{
				throw new ArgumentException(
					$"Cannot provide {nameof(maxValueCount)} of {maxValueCount} for {nameof(OptionValueType)}.{nameof(OptionValueType.None)}.",
					nameof(maxValueCount));
			}

			if (Array.IndexOf(Names, "<>") >= 0 &&
					((Names.Length == 1 && OptionValueType != OptionValueType.None) ||
					 (Names.Length > 1 && MaxValueCount > 1)))
			{
				throw new ArgumentException(
					"The default option handler '<>' cannot require values.",
					nameof(prototype));
			}
		}

		public string Prototype { get; }
		public string Description { get; }
		public OptionValueType OptionValueType { get; }
		public int MaxValueCount { get; }
		public bool Hidden { get; }

		public string[] GetNames()
		{
			return (string[])Names.Clone();
		}

		public string[] GetValueSeparators()
		{
			if (ValueSeparators is null)
			{
				return new string[0];
			}

			return (string[])ValueSeparators.Clone();
		}

		protected static T Parse<T>(string value, OptionContext c)
		{
			Type tt = typeof(T);
			Type ti = tt;

			bool nullable =
				ti.IsValueType &&
				ti.IsGenericType &&
				!ti.IsGenericTypeDefinition &&
				ti.GetGenericTypeDefinition() == typeof(Nullable<>);

			Type targetType = nullable ? tt.GetGenericArguments()[0] : tt;
			T t = default;
			try
			{
				if (value != null)
				{
					TypeConverter conv = TypeDescriptor.GetConverter(targetType);
					t = (T)conv.ConvertFromString(value);
				}
			}
			catch (Exception e)
			{
				throw new OptionException(
					string.Format(c.OptionSet.MessageLocalizer($"Could not convert string `{value}' to type {targetType.Name} for option `{c.OptionName}'.")),
					c.OptionName, e);
			}
			return t;
		}

		public string[] Names { get; }
		public string[] ValueSeparators { get; private set; }

		private static readonly char[] NameTerminator = new char[] { '=', ':' };

		private OptionValueType ParsePrototype()
		{
			char type = '\0';
			List<string> seps = new List<string>();
			for (int i = 0; i < Names.Length; ++i)
			{
				string name = Names[i];
				if (name == "")
				{
					throw new ArgumentException("Empty option names are not supported.", nameof(Prototype));
				}

				int end = name.IndexOfAny(NameTerminator);
				if (end == -1)
				{
					continue;
				}

				Names[i] = name.Substring(0, end);
				if (type == '\0' || type == name[end])
				{
					type = name[end];
				}
				else
				{
					throw new ArgumentException(
						$"Conflicting option types: '{type}' vs. '{name[end]}'.",
						nameof(Prototype));
				}

				AddSeparators(name, end, seps);
			}

			if (type == '\0')
			{
				return OptionValueType.None;
			}

			if (MaxValueCount <= 1)
			{
				if (seps.Count != 0)
				{
					throw new ArgumentException(
						$"Cannot provide key/value separators for Options taking {MaxValueCount} value(s).",
						nameof(Prototype));
				}
			}
			else
			{
				if (seps.Count == 0)
				{
					ValueSeparators = new string[] { ":", "=" };
				}
				else if (seps.Count == 1 && seps[0] == "")
				{
					ValueSeparators = null;
				}
				else
				{
					ValueSeparators = seps.ToArray();
				}
			}

			return type == '=' ? OptionValueType.Required : OptionValueType.Optional;
		}

		private static void AddSeparators(string name, int end, ICollection<string> seps)
		{
			int start = -1;
			for (int i = end + 1; i < name.Length; ++i)
			{
				switch (name[i])
				{
					case '{':
						if (start != -1)
						{
							throw new ArgumentException(
								$"Ill-formed name/value separator found in \"{name}\".",
								nameof(Prototype));
						}

						start = i + 1;
						break;

					case '}':
						if (start == -1)
						{
							throw new ArgumentException(
								$"Ill-formed name/value separator found in \"{name}\".",
								nameof(Prototype));
						}

						seps.Add(name.Substring(start, i - start));
						start = -1;
						break;

					default:
						if (start == -1)
						{
							seps.Add(name[i].ToString());
						}

						break;
				}
			}
			if (start != -1)
			{
				throw new ArgumentException(
					$"Ill-formed name/value separator found in \"{name}\".",
					nameof(Prototype));
			}
		}

		public void Invoke(OptionContext c)
		{
			OnParseComplete(c);
			c.OptionName = null;
			c.Option = null;
			c.OptionValues.Clear();
		}

		protected abstract void OnParseComplete(OptionContext c);

		public void InvokeOnParseComplete(OptionContext c)
		{
			OnParseComplete(c);
		}

		public override string ToString()
		{
			return Prototype;
		}
	}
}
