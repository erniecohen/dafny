using Microsoft.Dafny;

namespace DafnyCore.Test.Verification;

[Collection("Obligation translation")]
public class ObligationLoweringSoundnessTests {
  [Fact]
  public async Task GuardedSubsetIntroductionKeepsTheGuard() {
    const string source = "ghost predicate P(i: int) requires i != 0 { 10 / i > 0 } type S = i: int | i != 0 && P(i) witness 1 ghost function F(): S { 0 }";
    var text = ObligationFingerprint.Emit(await ObligationLoweringTests.Translate(source, true));
    Assert.Contains("P#canCall", text);
    Assert.Contains("==>", text);
    Assert.Contains("$Is", text);
  }

  [Fact]
  public async Task OwnFunctionPostconditionDoesNotBecomeAFreeSelfPermission() {
    const string source = "ghost function F(i: int): (r: int) ensures var unused := 1; F(i) == 0 { 1 }";
    var text = ObligationFingerprint.Emit(await ObligationLoweringTests.Translate(source, true));
    Assert.DoesNotContain("free ensures {:always_assume} _module.__default.F#canCall(i#0);", text);
  }
  [Fact]
  public async Task SynthesizedConstraintChecksKeepSourceDiagnosticLocations() {
    const string source="lemma L() { var n:nat := -1; }";
    var programs=await ObligationLoweringTests.Translate(source,true);
    var checks=programs.SelectMany(p=>p.Implementations).SelectMany(p=>p.Blocks)
      .SelectMany(b=>b.Cmds).OfType<Microsoft.Boogie.AssertCmd>().ToList();
    Assert.NotEmpty(checks);
    foreach (var check in checks) {
      var origin=BoogieGenerator.ToDafnyToken(check.tok);
      Assert.NotNull(origin.Uri);
      Assert.All(ErrorReporterExtensions.CreateDiagnosticRelatedInformationFor(origin,true),
        related=>Assert.NotNull(related.Range.Uri));
    }
  }

}
