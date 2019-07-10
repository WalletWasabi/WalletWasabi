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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MessageLocalizerConverter = System.Converter<string, string>;

namespace Mono.Options
{
	public class OptionSet : KeyedCollection<string, Option>
	{
		public OptionSet()
			: this(null)
		{
		}

		public OptionSet(MessageLocalizerConverter localizer)
		{
			ArgumentSources = new ReadOnlyCollection<ArgumentSource>(Sources);
			MessageLocalizer = localizer;
			if (MessageLocalizer is null)
			{
				MessageLocalizer = delegate (string f) {
					return f;
				};
			}
		}

		public MessageLocalizerConverter MessageLocalizer { get; set; }

		public ReadOnlyCollection<ArgumentSource> ArgumentSources { get; }
		public List<ArgumentSource> Sources { get; set; } = new List<ArgumentSource>();

		protected override string GetKeyForItem(Option item)
		{
			if (item is null)
			{
				throw new ArgumentNullException("option");
			}

			if (item.Names != null && item.Names.Length > 0)
			{
				return item.Names[0];
			}
			// This should never happen, as it is invalid for Option to be
			// constructed w/o any names.
			throw new InvalidOperationException("Option has no names!");
		}

		[Obsolete("Use KeyedCollection.this[string]")]
		protected Option GetOptionForName(string option)
		{
			if (option is null)
			{
				throw new ArgumentNullException(nameof(option));
			}

			try
			{
				return base[option];
			}
			catch (KeyNotFoundException)
			{
				return null;
			}
		}

		protected override void InsertItem(int index, Option item)
		{
			base.InsertItem(index, item);
			AddImpl(item);
		}

		protected override void RemoveItem(int index)
		{
			Option p = Items[index];
			base.RemoveItem(index);
			// KeyedCollection.RemoveItem() handles the 0th item
			for (int i = 1; i < p.Names.Length; ++i)
			{
				Dictionary.Remove(p.Names[i]);
			}
		}

		protected override void SetItem(int index, Option item)
		{
			base.SetItem(index, item);
			AddImpl(item);
		}

		private void AddImpl(Option option)
		{
			if (option is null)
			{
				throw new ArgumentNullException(nameof(option));
			}

			List<string> added = new List<string>(option.Names.Length);
			try
			{
				// KeyedCollection.InsertItem/SetItem handle the 0th name.
				for (int i = 1; i < option.Names.Length; ++i)
				{
					Dictionary.Add(option.Names[i], option);
					added.Add(option.Names[i]);
				}
			}
			catch (Exception)
			{
				foreach (string name in added)
				{
					Dictionary.Remove(name);
				}

				throw;
			}
		}

		public OptionSet Add(string header)
		{
			if (header is null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			Add(new Category(header));
			return this;
		}

		public sealed class Category : Option
		{
			// Prototype starts with '=' because this is an invalid prototype
			// (see Option.ParsePrototype(), and thus it'll prevent Category
			// instances from being accidentally used as normal options.
			public Category(string description)
				: base("=:Category:= " + description, description)
			{
			}

			protected override void OnParseComplete(OptionContext c)
			{
				throw new NotSupportedException("Category.OnParseComplete should not be invoked.");
			}
		}

		public new OptionSet Add(Option option)
		{
			base.Add(option);
			return this;
		}

		private sealed class ActionOption : Option
		{
			public ActionOption(string prototype, string description, int count, Action<OptionValueCollection> action)
				: this(prototype, description, count, action, false)
			{
			}

			public ActionOption(string prototype, string description, int count, Action<OptionValueCollection> action, bool hidden)
				: base(prototype, description, count, hidden)
			{
				Action = action ?? throw new ArgumentNullException(nameof(action));
			}

			public Action<OptionValueCollection> Action { get; set; }

			protected override void OnParseComplete(OptionContext c)
			{
				Action(c.OptionValues);
			}
		}

		public OptionSet Add(string prototype, Action<string> action)
		{
			return Add(prototype, null, action);
		}

		public OptionSet Add(string prototype, string description, Action<string> action)
		{
			return Add(prototype, description, action, false);
		}

		public OptionSet Add(string prototype, string description, Action<string> action, bool hidden)
		{
			if (action is null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			Option p = new ActionOption(prototype, description, 1,
					delegate (OptionValueCollection v) { action(v[0]); }, hidden);
			base.Add(p);
			return this;
		}

		public OptionSet Add(string prototype, OptionAction<string, string> action)
		{
			return Add(prototype, null, action);
		}

		public OptionSet Add(string prototype, string description, OptionAction<string, string> action)
		{
			return Add(prototype, description, action, false);
		}

		public OptionSet Add(string prototype, string description, OptionAction<string, string> action, bool hidden)
		{
			if (action is null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			Option p = new ActionOption(prototype, description, 2,
					delegate (OptionValueCollection v) { action(v[0], v[1]); }, hidden);
			base.Add(p);
			return this;
		}

		private sealed class ActionOption<T> : Option
		{
			public ActionOption(string prototype, string description, Action<T> action)
				: base(prototype, description, 1)
			{
				Action = action ?? throw new ArgumentNullException(nameof(action));
			}

			public Action<T> Action { get; set; }

			protected override void OnParseComplete(OptionContext c)
			{
				Action(Parse<T>(c.OptionValues[0], c));
			}
		}

		private sealed class ActionOption<TKey, TValue> : Option
		{
			public ActionOption(string prototype, string description, OptionAction<TKey, TValue> action)
				: base(prototype, description, 2)
			{
				Action = action ?? throw new ArgumentNullException(nameof(action));
			}

			public OptionAction<TKey, TValue> Action { get; set; }

			protected override void OnParseComplete(OptionContext c)
			{
				Action(
						Parse<TKey>(c.OptionValues[0], c),
						Parse<TValue>(c.OptionValues[1], c));
			}
		}

		public OptionSet Add<T>(string prototype, Action<T> action)
		{
			return Add(prototype, null, action);
		}

		public OptionSet Add<T>(string prototype, string description, Action<T> action)
		{
			return Add(new ActionOption<T>(prototype, description, action));
		}

		public OptionSet Add<TKey, TValue>(string prototype, OptionAction<TKey, TValue> action)
		{
			return Add(prototype, null, action);
		}

		public OptionSet Add<TKey, TValue>(string prototype, string description, OptionAction<TKey, TValue> action)
		{
			return Add(new ActionOption<TKey, TValue>(prototype, description, action));
		}

		public OptionSet Add(ArgumentSource source)
		{
			if (source is null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			Sources.Add(source);
			return this;
		}

		protected virtual OptionContext CreateOptionContext()
		{
			return new OptionContext(this);
		}

		public List<string> Parse(IEnumerable<string> arguments)
		{
			if (arguments is null)
			{
				throw new ArgumentNullException(nameof(arguments));
			}

			OptionContext c = CreateOptionContext();
			c.OptionIndex = -1;
			bool process = true;
			List<string> unprocessed = new List<string>();
			Option def = Contains("<>") ? this["<>"] : null;
			ArgumentEnumerator ae = new ArgumentEnumerator(arguments);
			foreach (string argument in ae)
			{
				++c.OptionIndex;
				if (argument == "--")
				{
					process = false;
					continue;
				}
				if (!process)
				{
					Unprocessed(unprocessed, def, c, argument);
					continue;
				}
				if (AddSource(ae, argument))
				{
					continue;
				}

				if (!Parse(argument, c))
				{
					Unprocessed(unprocessed, def, c, argument);
				}
			}
			if (c.Option != null)
			{
				c.Option.Invoke(c);
			}

			return unprocessed;
		}

		private class ArgumentEnumerator : IEnumerable<string>
		{
			public ArgumentEnumerator(IEnumerable<string> arguments)
			{
				Sources.Add(arguments.GetEnumerator());
			}

			public List<IEnumerator<string>> Sources { get; set; } = new List<IEnumerator<string>>();

			public void Add(IEnumerable<string> arguments)
			{
				Sources.Add(arguments.GetEnumerator());
			}

			public IEnumerator<string> GetEnumerator()
			{
				do
				{
					IEnumerator<string> c = Sources[Sources.Count - 1];
					if (c.MoveNext())
					{
						yield return c.Current;
					}
					else
					{
						c?.Dispose();
						Sources.RemoveAt(Sources.Count - 1);
					}
				} while (Sources.Count > 0);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		private bool AddSource(ArgumentEnumerator ae, string argument)
		{
			foreach (ArgumentSource source in Sources)
			{
				if (!source.GetArguments(argument, out IEnumerable<string> replacement))
				{
					continue;
				}

				ae.Add(replacement);
				return true;
			}
			return false;
		}

		private static bool Unprocessed(ICollection<string> extra, Option def, OptionContext c, string argument)
		{
			if (def is null)
			{
				extra.Add(argument);
				return false;
			}
			c.OptionValues.Add(argument);
			c.Option = def;
			c.Option.Invoke(c);
			return false;
		}

		private readonly Regex ValueOption = new Regex(
			@"^(?<flag>--|-|/)(?<name>[^:=]+)((?<sep>[:=])(?<value>.*))?$");

		protected bool GetOptionParts(string argument, out string flag, out string name, out string sep, out string value)
		{
			if (argument is null)
			{
				throw new ArgumentNullException(nameof(argument));
			}

			flag = name = sep = value = null;
			Match m = ValueOption.Match(argument);
			if (!m.Success)
			{
				return false;
			}
			flag = m.Groups[nameof(flag)].Value;
			name = m.Groups[nameof(name)].Value;
			if (m.Groups[nameof(sep)].Success && m.Groups[nameof(value)].Success)
			{
				sep = m.Groups[nameof(sep)].Value;
				value = m.Groups[nameof(value)].Value;
			}
			return true;
		}

		protected virtual bool Parse(string argument, OptionContext c)
		{
			if (c.Option != null)
			{
				ParseValue(argument, c);
				return true;
			}

			if (!GetOptionParts(argument, out string f, out string n, out string s, out string v))
			{
				return false;
			}

			Option p;
			if (Contains(n))
			{
				p = this[n];
				c.OptionName = f + n;
				c.Option = p;
				switch (p.OptionValueType)
				{
					case OptionValueType.None:
						c.OptionValues.Add(n);
						c.Option.Invoke(c);
						break;

					case OptionValueType.Optional:
					case OptionValueType.Required:
						ParseValue(v, c);
						break;
				}
				return true;
			}
			// no match; is it a bool option?
			if (ParseBool(argument, n, c))
			{
				return true;
			}
			// is it a bundled option?
			if (ParseBundledValue(f, string.Concat(n + s + v), c))
			{
				return true;
			}

			return false;
		}

		private void ParseValue(string option, OptionContext c)
		{
			if (option != null)
			{
				foreach (string o in c.Option.ValueSeparators != null
						? option.Split(c.Option.ValueSeparators, c.Option.MaxValueCount - c.OptionValues.Count, StringSplitOptions.None)
						: new string[] { option })
				{
					c.OptionValues.Add(o);
				}
			}

			if (c.OptionValues.Count == c.Option.MaxValueCount ||
					c.Option.OptionValueType == OptionValueType.Optional)
			{
				c.Option.Invoke(c);
			}
			else if (c.OptionValues.Count > c.Option.MaxValueCount)
			{
				throw new OptionException(MessageLocalizer(
						$"Error: Found {c.OptionValues.Count} option values when expecting {c.Option.MaxValueCount}."),
						c.OptionName);
			}
		}

		private bool ParseBool(string option, string n, OptionContext c)
		{
			Option p;
			string rn;
			if (n.Length >= 1 && (n[n.Length - 1] == '+' || n[n.Length - 1] == '-') &&
					Contains((rn = n.Substring(0, n.Length - 1))))
			{
				p = this[rn];
				string v = n[n.Length - 1] == '+' ? option : null;
				c.OptionName = option;
				c.Option = p;
				c.OptionValues.Add(v);
				p.Invoke(c);
				return true;
			}
			return false;
		}

		private bool ParseBundledValue(string f, string n, OptionContext c)
		{
			if (f != "-")
			{
				return false;
			}

			for (int i = 0; i < n.Length; ++i)
			{
				Option p;
				string opt = f + n[i].ToString();
				string rn = n[i].ToString();
				if (!Contains(rn))
				{
					if (i == 0)
					{
						return false;
					}

					throw new OptionException(string.Format(MessageLocalizer(
									"Cannot use unregistered option '{0}' in bundle '{1}'."), rn, f + n), null);
				}
				p = this[rn];
				switch (p.OptionValueType)
				{
					case OptionValueType.None:
						Invoke(c, opt, n, p);
						break;

					case OptionValueType.Optional:
					case OptionValueType.Required:
						{
							string v = n.Substring(i + 1);
							c.Option = p;
							c.OptionName = opt;
							ParseValue(v.Length != 0 ? v : null, c);
							return true;
						}
					default:
						throw new InvalidOperationException("Unknown OptionValueType: " + p.OptionValueType);
				}
			}
			return true;
		}

		private static void Invoke(OptionContext c, string name, string value, Option option)
		{
			c.OptionName = name;
			c.Option = option;
			c.OptionValues.Add(value);
			option.Invoke(c);
		}

		private const int OptionWidth = 29;
		private const int DescriptionFirstWidth = 140 - OptionWidth;
		private const int DescriptionRemWidth = 140 - OptionWidth - 2;

		private static readonly string CommandHelpIndentStart = new string(' ', OptionWidth);
		private static readonly string CommandHelpIndentRemaining = new string(' ', OptionWidth + 2);

		public void WriteOptionDescriptions(TextWriter o)
		{
			foreach (Option p in this)
			{
				int written = 0;

				if (p.Hidden)
				{
					continue;
				}

				if (p is Category c)
				{
					WriteDescription(o, p.Description, "", 140, 140);
					continue;
				}
				if (p is CommandOption co)
				{
					WriteCommandDescription(o, co.Command, co.CommandName);
					continue;
				}

				if (!WriteOptionPrototype(o, p, ref written))
				{
					continue;
				}

				if (written < OptionWidth)
				{
					o.Write(new string(' ', OptionWidth - written));
				}
				else
				{
					o.WriteLine();
					o.Write(new string(' ', OptionWidth));
				}

				WriteDescription(o, p.Description, new string(' ', OptionWidth + 2),
						DescriptionFirstWidth, DescriptionRemWidth);
			}

			foreach (ArgumentSource s in Sources)
			{
				string[] names = s.GetNames();
				if (names is null || names.Length == 0)
				{
					continue;
				}

				int written = 0;

				Write(o, ref written, "  ");
				Write(o, ref written, names[0]);
				for (int i = 1; i < names.Length; ++i)
				{
					Write(o, ref written, ", ");
					Write(o, ref written, names[i]);
				}

				if (written < OptionWidth)
				{
					o.Write(new string(' ', OptionWidth - written));
				}
				else
				{
					o.WriteLine();
					o.Write(new string(' ', OptionWidth));
				}

				WriteDescription(o, s.Description, new string(' ', OptionWidth + 2),
						DescriptionFirstWidth, DescriptionRemWidth);
			}
		}

		public void WriteCommandDescription(TextWriter o, Command c, string commandName)
		{
			var name = new string(' ', 8) + (commandName ?? c.Name);
			if (name.Length < OptionWidth - 1)
			{
				WriteDescription(o, name + new string(' ', OptionWidth - name.Length) + c.Help, CommandHelpIndentRemaining, 140, DescriptionRemWidth);
			}
			else
			{
				WriteDescription(o, name, "", 140, 140);
				WriteDescription(o, CommandHelpIndentStart + c.Help, CommandHelpIndentRemaining, 140, DescriptionRemWidth);
			}
		}

		private void WriteDescription(TextWriter o, string value, string prefix, int firstWidth, int remWidth)
		{
			bool indent = false;
			foreach (string line in GetLines(MessageLocalizer(GetDescription(value)), firstWidth, remWidth))
			{
				if (indent)
				{
					o.Write(prefix);
				}

				o.WriteLine(line);
				indent = true;
			}
		}

		private bool WriteOptionPrototype(TextWriter o, Option p, ref int written)
		{
			string[] names = p.Names;

			int i = GetNextOptionIndex(names, 0);
			if (i == names.Length)
			{
				return false;
			}

			if (names[i].Length == 1)
			{
				Write(o, ref written, "  -");
				Write(o, ref written, names[0]);
			}
			else
			{
				Write(o, ref written, "      --");
				Write(o, ref written, names[0]);
			}

			for (i = GetNextOptionIndex(names, i + 1);
					i < names.Length; i = GetNextOptionIndex(names, i + 1))
			{
				Write(o, ref written, ", ");
				Write(o, ref written, names[i].Length == 1 ? "-" : "--");
				Write(o, ref written, names[i]);
			}

			if (p.OptionValueType == OptionValueType.Optional ||
					p.OptionValueType == OptionValueType.Required)
			{
				if (p.OptionValueType == OptionValueType.Optional)
				{
					Write(o, ref written, MessageLocalizer("["));
				}
				Write(o, ref written, MessageLocalizer("=" + GetArgumentName(0, p.MaxValueCount, p.Description)));
				string sep = p.ValueSeparators != null && p.ValueSeparators.Length > 0
					? p.ValueSeparators[0]
					: " ";
				for (int c = 1; c < p.MaxValueCount; ++c)
				{
					Write(o, ref written, MessageLocalizer(sep + GetArgumentName(c, p.MaxValueCount, p.Description)));
				}
				if (p.OptionValueType == OptionValueType.Optional)
				{
					Write(o, ref written, MessageLocalizer("]"));
				}
			}
			return true;
		}

		private static int GetNextOptionIndex(string[] names, int i)
		{
			while (i < names.Length && names[i] == "<>")
			{
				++i;
			}
			return i;
		}

		private static void Write(TextWriter o, ref int n, string s)
		{
			n += s.Length;
			o.Write(s);
		}

		private static string GetArgumentName(int index, int maxIndex, string description)
		{
			var matches = Regex.Matches(description ?? "", @"(?<=(?<!\{)\{)[^{}]*(?=\}(?!\}))"); // ignore double braces
			string argName = "";
			foreach (Match match in matches)
			{
				var parts = match.Value.Split(':');
				// for maxIndex=1 it can be {foo} or {0:foo}
				if (maxIndex == 1)
				{
					argName = parts[parts.Length - 1];
				}
				// look for {i:foo} if maxIndex > 1
				if (maxIndex > 1 && parts.Length == 2 &&
					parts[0] == index.ToString(CultureInfo.InvariantCulture))
				{
					argName = parts[1];
				}
			}

			if (string.IsNullOrEmpty(argName))
			{
				argName = maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
			}
			return argName;
		}

		private static string GetDescription(string description)
		{
			if (description is null)
			{
				return string.Empty;
			}

			StringBuilder sb = new StringBuilder(description.Length);
			int start = -1;
			for (int i = 0; i < description.Length; ++i)
			{
				switch (description[i])
				{
					case '{':
						if (i == start)
						{
							sb.Append('{');
							start = -1;
						}
						else if (start < 0)
						{
							start = i + 1;
						}

						break;

					case '}':
						if (start < 0)
						{
							if ((i + 1) == description.Length || description[i + 1] != '}')
							{
								throw new InvalidOperationException("Invalid option description: " + description);
							}

							++i;
							sb.Append("}");
						}
						else
						{
							sb.Append(description.Substring(start, i - start));
							start = -1;
						}
						break;

					case ':':
						if (start < 0)
						{
							goto default;
						}

						start = i + 1;
						break;

					default:
						if (start < 0)
						{
							sb.Append(description[i]);
						}

						break;
				}
			}
			return sb.ToString();
		}

		private static IEnumerable<string> GetLines(string description, int firstWidth, int remWidth)
		{
			return StringCoda.WrappedLines(description, firstWidth, remWidth);
		}
	}
}
