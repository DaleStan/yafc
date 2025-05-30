﻿using Xunit;
using Yafc.I18n;

namespace Yafc.Model.Data.Tests;

public class LocalisedStringParserTests {
    public LocalisedStringParserTests() => FactorioLocalization.Initialize(new System.Collections.Generic.Dictionary<string, string>() {
        ["hours"] = "__1__ __plural_for_parameter__1__{1=hour|rest=hours}__",
        ["si-unit-kilometer-per-hour"] = "__1__ km/h",
        ["not-enough-ingredients"] = "Not enough ingredients.",
        ["item-name.iron-plate"] = "Iron plate",
        ["item-name.big-iron-plate"] = "Big __ITEM__iron-plate__",
        ["connecting"] = "__plural_for_parameter__1__{1=__1__ player is|rest=__1__ players are}__ connecting",
        ["ends.in"] = "__plural_for_parameter__1__{ends in 12=option 1|ends in 2=option 2|rest=option 3}__"
    });

    [Fact]
    public void Parse_JustString() {
        string localised = LocalisedStringParser.ParseObject("test");
        Assert.Equal("test", localised);
    }

    [Fact]
    public void Parse_RemoveRichText() {
        string localised = LocalisedStringParser.ParseObject("[color=#ffffff]iron[/color] [color=1,0,0]plate[.color] [item=iron-plate]");
        Assert.Equal("iron plate ", localised);
    }

    [Fact]
    public void Parse_NoParameters() {
        string localised = LocalisedStringParser.ParseKey("not-enough-ingredients", []);
        Assert.Equal("Not enough ingredients.", localised);
    }

    [Fact]
    public void Parse_Parameter() {
        string localised = LocalisedStringParser.ParseKey("si-unit-kilometer-per-hour", ["100"]);
        Assert.Equal("100 km/h", localised);
    }

    [Fact]
    public void Parse_LinkItem() {
        string localised = LocalisedStringParser.ParseKey("item-name.big-iron-plate", []);
        Assert.Equal("Big Iron plate", localised);
    }

    [Fact]
    public void Parse_PluralSpecial() {
        string localised = LocalisedStringParser.ParseKey("hours", ["1"]);
        Assert.Equal("1 hour", localised);
    }

    [Fact]
    public void Parse_PluralRest() {
        string localised = LocalisedStringParser.ParseKey("hours", ["2"]);
        Assert.Equal("2 hours", localised);
    }

    [Fact]
    public void Parse_PluralWithParameter() {
        string localised = LocalisedStringParser.ParseKey("connecting", ["1"]);
        Assert.Equal("1 player is connecting", localised);
    }

    [Theory]
    [InlineData(12, "option 1")]
    [InlineData(22, "option 2")]
    [InlineData(5, "option 3")]
    public void Parse_PluralEndsIn(int n, string expectedResult) {
        string localised = LocalisedStringParser.ParseKey("ends.in", [n.ToString()]);
        Assert.Equal(expectedResult, localised);
    }
}
