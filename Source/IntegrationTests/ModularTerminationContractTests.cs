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

  // https://github.com/erniecohen/dafny/issues/266, M4 (BC41): replacing the private body of a certified function, of
  // the factory that certifies it, or of a helper that the body calls keeps the client's Boogie input exactly; the
  // client sees the bounded value's type, its bound in the factory's contract, and nothing of the body.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PrivateCertifiedBodiesPreserveExactClientTranslation(bool consistent) {
    const string source = """
      module Library {
        class {:termination_closed} Cell {
          const {:termination_closed} f: bounded<nat, int -> int>
          constructor (f: bounded<nat, int -> int>) ensures this.f == f decreases {:global} 0 { this.f := f; }
        }
        method Helper(x: int) returns (r: int) decreases {:global} 1 { HELPER }
        function Step(x: int): int decreases {:global} 1 { x }
        method Make(n: int) returns (c: Cell) ensures c.f.bound == 2 decreases {:global} 3 {
          FACTORY
          c := new Cell(bounded<nat, int -> int>.certify(2, (x: int) => BODY));
        }
      }
      module Client {
        import Library
        method Use() decreases {:global} 4 { var c := Library.Make(0); var r := c.f(1); }
      }
      """;
    var baseline = await ClientTranslation(source.Replace("HELPER", "r := x;").Replace("FACTORY", "").Replace("BODY", "x + 1"), true, consistent, true);
    foreach (var (helper, factory, body) in new[] {
               ("r := x + 1;", "", "x + 1"),
               ("r := x;", "var m := Helper(n);", "x + 1"),
               ("r := x;", "", "x + 2"),
               ("r := x;", "", "Step(x)"),
               ("r := x;", "var m := Helper(n);", "Step(Step(x))"),
             }) {
      var replacement = source.Replace("HELPER", helper).Replace("FACTORY", factory).Replace("BODY", body);
      Assert.Equal(baseline, await ClientTranslation(replacement, true, consistent, true));
    }
    // Negative controls: the bound in the factory's contract, and the order of the bounded type, are the client's
    var lower = source.Replace("HELPER", "r := x;").Replace("FACTORY", "").Replace("BODY", "x + 1")
      .Replace("c.f.bound == 2", "c.f.bound == 1").Replace("certify(2,", "certify(1,");
    Assert.NotEqual(baseline, await ClientTranslation(lower, true, consistent, true));
    var ordinal = source.Replace("HELPER", "r := x;").Replace("FACTORY", "").Replace("BODY", "x + 1")
      .Replace("bounded<nat,", "bounded<ORDINAL,");
    Assert.NotEqual(baseline, await ClientTranslation(ordinal, true, consistent, true));
  }

  private static async Task<string> ClientTranslation(string source, bool refresh, bool consistent, bool generalTraits = false) {
    var output = new StringWriter();
    var options = new DafnyOptions(TextReader.Null, output, output);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.ModularTermination, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refresh);
    options.Set(CommonOptionBag.ConsistentObligationChecks, consistent);
    if (generalTraits) {
      options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
    }
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
