using System.Text.RegularExpressions;
using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test.Resolver;

// https://github.com/erniecohen/dafny/issues/422: under the type system refresh, the witness of a newtype
// without a constraint variable was not checked by the UnderspecificationDetector, which is where the
// refresh fills in each binary expression's ResolvedOp.  TypeRefinementVisitor then read an undetermined
// ResolvedOp, which in a Debug build fails the assertion in BinaryExpr.ResolvedOp and ends the dafny process;
// an underspecified witness failed a cast in the same build instead of reporting an error.  Release builds
// compile the assertion out and did not fail.  So review.yml runs these tests in its Debug step, where the
// test host turns the failed assertion into a failed test.
[Collection("Cardinality resolution")]
public class NewtypeWitnessResolutionTests {
  private static async Task<(Program Program, BatchErrorReporter Reporter)> Resolve(string source, bool extended) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.GeneralNewtypes, extended);
    options.Set(CommonOptionBag.ExtendedNewtypeBases, extended);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(source, new Uri("untitled:issue422.dfy"), reporter);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    return (parsed.Program, reporter);
  }

  private static string Diagnostics(BatchErrorReporter reporter) =>
    string.Join(Environment.NewLine, reporter.AllMessages.Select(diagnostic => diagnostic.Message));

  private static IEnumerable<BinaryExpr> BinaryExpressions(Expression expression) =>
    (expression is BinaryExpr binary ? new[] { binary } : Enumerable.Empty<BinaryExpr>())
    .Concat(expression.SubExpressions.SelectMany(BinaryExpressions));

  [Theory]
  [InlineData("newtype N = int witness 1 + 1", false)]
  [InlineData("newtype Inc = int -> int witness ((x: int) => x + 1)", true)]
  [InlineData("newtype Binary = (int, int) -> int witness ((x: int, y: int) => x + y)", true)]
  public async Task WitnessWithoutConstraintVariableResolvesItsOperators(string source, bool extended) {
    var (program, reporter) = await Resolve(source, extended);
    Assert.True(reporter.ErrorCount == 0, Diagnostics(reporter));
    var newtype = Assert.Single(program.DefaultModuleDef.TopLevelDecls.OfType<NewtypeDecl>());
    Assert.Null(newtype.Var);
    var addition = Assert.Single(BinaryExpressions(newtype.Witness));
    Assert.Equal(BinaryExpr.ResolvedOpcode.Add, addition.ResolvedOp);
  }

  // The same underspecified witness is reported the same way with and without a constraint variable,
  // and for a subset type.
  [Theory]
  [InlineData("newtype N = int witness (var s := {}; 0)")]
  [InlineData("newtype N = n: int | true witness (var s := {}; 0)")]
  [InlineData("type S = x: int | true witness (var s := {}; 0)")]
  public async Task UnderspecifiedWitnessIsReported(string source) {
    var (_, reporter) = await Resolve(source, false);
    var errors = reporter.AllMessagesByLevel[ErrorLevel.Error]
      .Select(diagnostic => Regex.Replace(diagnostic.Message, @"\?\d+", "?"));
    Assert.Equal(new[] {
      "the type ('set<?>') of this expression is underspecified",
      "type of bound variable 's' could not be determined ('set<?>'); please specify the type explicitly"
    }, errors);
  }
}
