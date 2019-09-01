using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class LabelTests
	{
		[Fact]
		public void LabelParsingTests()
		{
			var label = new Label();
			Assert.Equal("", label.ToString());

			label = new Label("");
			Assert.Equal("", label.ToString());

			label = new Label(null);
			Assert.Equal("", label.ToString());

			label = new Label(null, null);
			Assert.Equal("", label.ToString());

			label = new Label(" ");
			Assert.Equal("", label.ToString());

			label = new Label(",");
			Assert.Equal("", label.ToString());

			label = new Label(":");
			Assert.Equal("", label.ToString());

			label = new Label("foo");
			Assert.Equal("foo", label.ToString());

			label = new Label("foo", "bar");
			Assert.Equal("bar, foo", label.ToString());

			label = new Label("foo bar");
			Assert.Equal("foo bar", label.ToString());

			label = new Label("foo bar", "Buz quX@");
			Assert.Equal("Buz quX@, foo bar", label.ToString());

			label = new Label(new List<string>() { "foo", "bar" });
			Assert.Equal("bar, foo", label.ToString());

			label = new Label("  foo    ");
			Assert.Equal("foo", label.ToString());

			label = new Label("foo      ", "      bar");
			Assert.Equal("bar, foo", label.ToString());

			label = new Label(new List<string>() { "   foo   ", "   bar    " });
			Assert.Equal("bar, foo", label.ToString());

			label = new Label(new List<string>() { "foo:", ":bar", null, ":buz:", ",", ": , :", "qux:quux", "corge,grault", "", "  ", " , garply, waldo,", " : ,  :  ,  fred  , : , :   plugh, : , : ," });
			Assert.Equal("bar, buz, corge, foo, fred, garply, grault, plugh, quux, qux, waldo", label.ToString());

			label = new Label(",: foo::bar :buz:,: , :qux:quux, corge,grault  , garply, waldo, : ,  :  ,  fred  , : , :   plugh, : , : ,");
			Assert.Equal("bar, buz, corge, foo, fred, garply, grault, plugh, quux, qux, waldo", label.ToString());
		}

		[Fact]
		public void LabelEqualityTests()
		{
			var label = new Label("foo");
			var label2 = new Label(label.ToString());
			Assert.Equal(label, label2);

			label = new Label("foo, bar, buz");
			label2 = new Label(label.ToString());
			Assert.Equal(label, label2);

			label2 = new Label("bar, buz, foo");
			Assert.Equal(label, label2);

			var label3 = new Label("bar, buz");
			Assert.NotEqual(label, label3);

			Label label4 = null;
			Label label5 = null;
			Assert.Equal(label4, label5);
			Assert.NotEqual(label, label4);
			Assert.False(label.Equals(label4));
			Assert.False(label == label4);
		}

		[Fact]
		public void SpecialLabelTests()
		{
			var label = new Label("");
			Assert.Equal(label, Label.Empty);
			Assert.True(label.IsEmpty);

			label = new Label("foo, bar, buz");
			var label2 = new Label("qux");

			label = Label.Merge(label, label2);
			Assert.Equal("bar, buz, foo, qux", label.ToString());

			label2 = new Label("qux", "bar");
			label = Label.Merge(label, label2);
			Assert.Equal(4, label.Labels.Count());
			Assert.Equal("bar, buz, foo, qux", label.ToString());

			label2 = new Label("Qux", "Bar");
			label = Label.Merge(label, label2, null);
			Assert.Equal(4, label.Labels.Count());
			Assert.Equal("bar, buz, foo, qux", label.ToString());
		}
	}
}
