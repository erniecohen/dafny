using System.Text.RegularExpressions;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test.Resolver;

// https://github.com/erniecohen/dafny/issues/392
public class ModuleNamePrefixesTests {
  private static readonly Regex Shape = new("^([A-Z][A-Za-z0-9]*_)?$");

  private static readonly string[] Names = [
    "_module", "_System", "_System.tuple3", "_System.array2", "_System.bv8", "_System._tuple#2",
    "", "A", "M0", "Foo", "Foob", "Foo.BarBaz", "Std.JSON.ZeroCopy.Deserializer.Sequences",
    "Ünïcödé.模块", new string('L', 10_000), new string('L', 10_001),
  ];

  [Fact]
  public void PrefixesAreShortDelimitedAndDistinct() {
    var table = ModuleNamePrefixes.For(Names);
    var prefixes = Names.Select(table.Prefix).ToList();
    Assert.All(prefixes, p => Assert.Matches(Shape, p));
    Assert.Equal(prefixes.Count, prefixes.Distinct().Count());
    Assert.Equal("", table.Prefix("_module"));
    Assert.Equal("S_", table.Prefix("_System"));
    Assert.Equal("Stuple3_", table.Prefix("_System.tuple3"));
    Assert.Equal("X_", table.Prefix(""));
    Assert.Equal("X4d30_", table.Prefix("M0"));
    // A name longer than three bytes costs at most "M", the digest start and "_", however long it is.
    foreach (var name in new[] { "Foob", "Foo.BarBaz", new string('L', 10_000), "Ünïcödé.模块" }) {
      Assert.Matches("^M[a-z2-7]{6}_$", table.Prefix(name));
    }
  }

  [Fact]
  public void GeneratedNamesOfDistinctPrefixesNeverMeet() {
    // A scope's names are kind + prefix + counter, with counters in [0-9_]*.
    var table = ModuleNamePrefixes.For(Names);
    var prefixes = Names.Select(table.Prefix).Distinct().ToList();
    string[] counters = ["0", "1", "10", "1_0", "0_1_2", "12_3"];
    var generated = new Dictionary<string, string>();
    foreach (var prefix in prefixes) {
      foreach (var counter in counters) {
        var name = "x#" + prefix + counter;
        if (generated.TryGetValue(name, out var other)) {
          Assert.Equal(other, prefix);
        }
        generated[name] = prefix;
      }
    }
  }

  [Fact]
  public void PrefixesDependOnTheSetNotTheOrder() {
    var forward = ModuleNamePrefixes.For(Names);
    var backward = ModuleNamePrefixes.For(Names.Reverse().Concat(Names));
    Assert.All(Names, name => Assert.Equal(forward.Prefix(name), backward.Prefix(name)));
  }

  [Fact]
  public void TheSameModuleInTwoProgramsKeepsItsPrefix() {
    var one = ModuleNamePrefixes.For(["Library", "ClientOne"]);
    var two = ModuleNamePrefixes.For(["Library", "ClientTwo", "Another"]);
    Assert.Equal(one.Prefix("Library"), two.Prefix("Library"));
    Assert.Equal("M" + ModuleNamePrefixes.Digest("Library")[..6] + "_", one.Prefix("Library"));
  }

  [Fact]
  public void CollidingDigestsAreLengthenedOrSpelledOut() {
    var digests = new Dictionary<string, string> {
      ["Alpha"] = "aaaaaaab" + new string('q', 44),
      ["Bravo"] = "aaaaaaac" + new string('q', 44),
      ["Charlie"] = "aaaaaaab" + new string('r', 44),
      ["Delta"] = "bbbbbbbbbbbb",
      ["Echo"] = "bbbbbbbbbbbb",
      ["Foxtrot"] = "cccccc" + new string('d', 46),
    };
    var table = new ModuleNamePrefixes(digests.Keys, name => digests[name], ModuleNamePrefixes.MinimumLength);
    // Bravo leaves the group at the eighth character; Alpha and Charlie at the ninth.
    Assert.Equal("Maaaaaaac_", table.Prefix("Bravo"));
    Assert.Equal("Maaaaaaabq_", table.Prefix("Alpha"));
    Assert.Equal("Maaaaaaabr_", table.Prefix("Charlie"));
    // Whole digests that agree fall back to the injective hex spelling.
    Assert.Equal("X44656c7461_", table.Prefix("Delta"));
    Assert.Equal("X4563686f_", table.Prefix("Echo"));
    // A name that collides with nothing keeps the shortest prefix.
    Assert.Equal("Mcccccc_", table.Prefix("Foxtrot"));
    // Without the colliding names, Alpha's prefix is short again; the order of the names never matters.
    var alone = new ModuleNamePrefixes(["Alpha", "Foxtrot"], name => digests[name], ModuleNamePrefixes.MinimumLength);
    Assert.Equal("Maaaaaa_", alone.Prefix("Alpha"));
    var reversed = new ModuleNamePrefixes(digests.Keys.Reverse(), name => digests[name], ModuleNamePrefixes.MinimumLength);
    Assert.All(digests.Keys, name => Assert.Equal(table.Prefix(name), reversed.Prefix(name)));
    var all = digests.Keys.Select(table.Prefix).ToList();
    Assert.Equal(all.Count, all.Distinct().Count());
  }

  [Fact]
  public void ANameOutsideTheTableIsSpelledOut() {
    var table = ModuleNamePrefixes.For(["Known"]);
    Assert.Equal("X556e6b6e6f776e_", table.Prefix("Unknown"));
  }

  [Fact]
  public void TheDigestIsBase32OfSha256() {
    var digest = ModuleNamePrefixes.Digest("");
    Assert.Equal(52, digest.Length);
    // SHA-256("") = e3b0c442..., whose first 30 bits are 11100 01110 11000 01100 01000 10000.
    Assert.StartsWith("4oymiq", digest);
  }
}
