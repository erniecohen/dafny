using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace IntegrationTests;

[Collection("Literal identity translation")]
public class ModularTerminationContractTests {
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task PrivateBodiesAndHelpersPreserveExactClientTranslation(bool refresh, bool consistent) {
    const string source = """
      module Library {
        export reveals T provides T.Step, H, Closed, Fixed, Weight, Small
        trait {:termination false} T { method Step() decreases {:global} 0 }
        method H(t: T) decreases {:global} 1 { BODY }
        method Private(t: T) decreases {:global} 1 { PRIVATE }
        EXTRA
        method {:termination_closed} Closed() { CLOSED }
        function {:termination_closed} Fixed(): nat { VALUE }
        const {:termination_closed} Weight: nat := VALUE
        type {:termination_closed} Small = x: int | CONSTRAINT witness 1
      }
      module Client {
        import Library
        method Call(t: Library.T) decreases {:global} 1 { Library.H(t); Library.Closed(); var n := Library.Fixed(); var w := Library.Weight; }
      }
      """;
    var baseline = await ClientTranslation(source.Replace("BODY", "").Replace("PRIVATE", "").Replace("EXTRA", "").Replace("CLOSED", "").Replace("VALUE", "0").Replace("CONSTRAINT", "0 <= x"), refresh, consistent);
    foreach (var body in new[] { "t.Step();", "Private(t);" }) {
      var replacement = source.Replace("BODY", body).Replace("PRIVATE", "t.Step();").Replace("EXTRA", "method Hidden(t: T) decreases {:global} 1 { t.Step(); }").Replace("CLOSED", "var n := 1;").Replace("VALUE", "1").Replace("CONSTRAINT", "1 <= x");
      Assert.Equal(baseline, await ClientTranslation(replacement, refresh, consistent));
    }
  }

  private static async Task<string> ClientTranslation(string source, bool refresh, bool consistent) {
    var output = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ModularTermination, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.ConsistentObligationChecks, consistent);
    Microsoft.Dafny.Type.ResetScopes();
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:stable-contracts"), reporter);
    DafnyMain.Resolve(parsed.Program);
    Assert.True(reporter.ErrorCount == 0, output.ToString());
    var programs = BoogieGenerator.Translate(parsed.Program, reporter).ToList();
    var client = Assert.Single(programs.Where(p => p.Item1 == "Client")).Item2;
    var text = new StringWriter();
    client.Emit(new Bpl.TokenTextWriter("virtual", text, true, options));
    return text.ToString(); // Exact bytes/order/identifiers; no alpha renaming or normalization.
  }
}
