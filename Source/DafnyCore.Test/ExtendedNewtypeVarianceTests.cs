// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class ExtendedNewtypeVarianceTests {
  private const string InvarianceDiagnostic =
    "a constrained newtype with an extended base only supports invariant type parameters";

  // Resolution tests only: rejecting a declaration is the intended boundary.
  // A later verification failure is not evidence that this guard ran.
  [Theory]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<+T> = Box<T> witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<*T> = Box<T> witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<T> = b: Box<T> | true witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<!T> = b: Box<T> | true witness *")]
  [InlineData("newtype N<+T> = (T, int) witness *")]
  [InlineData("newtype N<+T> = (ghost T, int) witness *")]
  [InlineData("codatatype Stream<+T> = S(value: T, tail: Stream<T>) newtype N<+T> = Stream<T> witness *")]
  [InlineData("codatatype Stream<+T> = S(value: T, tail: Stream<T>) newtype N<T> = s: Stream<T> | true witness *")]
  [InlineData("datatype Consumer<-T> = C(callback: T -> int) newtype N<-T> = Consumer<T> witness *")]
  [InlineData("newtype N<+T> = ORDINAL witness *")]
  [InlineData("newtype N<T> = o: ORDINAL | true witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype Base<+T> = Box<T> witness * type Alias<+T> = Base<T> newtype N<T> = b: Alias<T> | true witness *")]
  [InlineData("newtype N<T> = x: T | true witness *")]
  public async Task AllowsUnconstrainedVarianceAndConstrainedInvariance(string source) {
    var reporter = await ResolveAsync(source);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
  }

  [Theory]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<+T> = b: Box<T> | true witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<*T> = b: Box<T> | true witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<+T(!new)> = b: Box<T> | (forall t: T :: false) witness *")]
  [InlineData("newtype N<+T> = p: (T, int) | true witness *")]
  [InlineData("newtype N<*T> = p: (ghost T, int) | true witness *")]
  [InlineData("codatatype Stream<+T> = S(value: T, tail: Stream<T>) newtype N<+T> = s: Stream<T> | true witness *")]
  [InlineData("datatype Consumer<-T> = C(callback: T -> int) newtype N<-T> = c: Consumer<T> | true witness *")]
  [InlineData("newtype N<+T> = o: ORDINAL | true witness *")]
  [InlineData("newtype N<-T> = o: ORDINAL | true witness *")]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype Base<+T> = Box<T> witness * type Alias<+T> = Base<T> newtype N<+T> = b: Alias<T> | true witness *")]
  [InlineData("newtype N<+T> = x: T | true witness *")]
  public async Task RejectsUnsupportedPredicateVariance(string source) {
    var reporter = await ResolveAsync(source);
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
      diagnostic.Message.Contains(InvarianceDiagnostic));
  }

  [Theory]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype N<-T> = Box<T> witness *")]
  [InlineData("datatype Consumer<-T> = C(callback: T -> int) newtype N<*T> = Consumer<T> witness *")]
  public async Task UnconstrainedWrappersStillCheckTheBasePositions(string source) {
    var reporter = await ResolveAsync(source);
    Assert.Contains(reporter.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
      diagnostic.Message.Contains("not used according to its variance specification"));
  }

  [Theory]
  [InlineData("datatype Box<+T> = Empty | Full(value: T) newtype Base<+T> = Box<T> witness *")]
  [InlineData("newtype Base<+T> = ORDINAL witness *")]
  public async Task ProvidedBasesDoNotBypassPredicateInvariance(string providerDeclarations) {
    foreach (var provided in new[] { false, true }) {
      var prefix = $$"""
        module Provider {
          {{providerDeclarations}}
          export API {{(provided ? "provides" : "reveals")}} Base {{(providerDeclarations.StartsWith("datatype ") ? "reveals Box" : "")}}
        }
        module Client {
          import P = Provider`API
        """ + Environment.NewLine;
      var rejected = await ResolveAsync(prefix +
        "newtype N<+T> = b: P.Base<T> | true witness * }");
      Assert.Contains(rejected.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
        diagnostic.Message.Contains(InvarianceDiagnostic));
      var invariant = await ResolveAsync(prefix +
        "newtype N<T> = b: P.Base<T> | true witness * }");
      Assert.True(invariant.ErrorCount == 0, Diagnostics(invariant));
      var unconstrained = await ResolveAsync(prefix +
        "newtype N<+T> = P.Base<T> witness * }");
      Assert.True(unconstrained.ErrorCount == 0, Diagnostics(unconstrained));
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RetainsExistingGeneralNewtypeCarrierVariance(bool enabled) {
    // These are existing collection bases. The extension does not redesign
    // the established variance rules when the carrier is known to be old.
    const string source = """
      newtype Produce<+T> = s: seq<int -> T> | true witness *
      newtype Consume<-T> = s: seq<T -> int> | true witness *
      """;
    var reporter = await ResolveAsync(source, enabled);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
  }

  private static async Task<BatchErrorReporter> ResolveAsync(string source, bool enabled = true) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.GeneralNewtypes, true);
    options.Set(CommonOptionBag.ExtendedNewtypeBases, enabled);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:issue113-variance.dfy"), reporter);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    return reporter;
  }

  private static string Diagnostics(BatchErrorReporter reporter) =>
    string.Join(Environment.NewLine, reporter.AllMessages.Select(diagnostic => diagnostic.Message));
}
