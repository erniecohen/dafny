using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Refinement;

public class RefinementTests : ClientBasedLanguageServerTest {
  [Fact]
  public async Task RefinedLemmaNewEnsuresIsVerified() {
    var source = @"
module A {

  lemma Test()
  function F(): int
}

module B refines A {

  lemma Test()
    ensures false
  {
  }

  function F(): int
    ensures false { 3 }

}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    Assert.Equal(2, diagnostics.Length);
  }

  // https://github.com/erniecohen/dafny/issues/404: a refining module that strengthens a trait member makes the
  // override check of the class member it inherits unchanged.  The error is at the inherited member, where the
  // base module's own tasks for that member also report theirs.
  [Fact]
  public async Task StrengthenedTraitMemberChecksInheritedOverride() {
    var source = @"
module A {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
module B refines A {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := new C(); s.L(); }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    var diagnostic = Assert.Single(diagnostics);
    Assert.Contains("more detailed postcondition", diagnostic.Message);
    Assert.Equal(2, diagnostic.Range.Start.Line);
  }

  [Fact]
  public async Task InheritedOverrideCheckKeepsTheBaseMembersErrors() {
    var source = @"
module A {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { assert 1 == 2; } }
}
module B refines A {
  trait S ... { lemma L... ensures false }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    Assert.Equal(2, diagnostics.Length);
    Assert.Contains(diagnostics, d => d.Message.Contains("more detailed postcondition"));
    Assert.Contains(diagnostics, d => d.Message.Contains("assertion might not hold"));
    Assert.All(diagnostics, d => Assert.Equal(2, d.Range.Start.Line));
  }

  // The override check fails in A.  B inherits the pair unchanged, so only A reports it.
  [Fact]
  public async Task UnchangedOverridePairIsNotCheckedAgain() {
    var source = @"
module A {
  trait S { lemma L() ensures 1 == 2 }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
module B refines A {
  trait S ... { lemma Extra() { } }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    var diagnostic = Assert.Single(diagnostics);
    Assert.Contains("more detailed postcondition", diagnostic.Message);
    Assert.Equal(2, diagnostic.Range.Start.Line);
    Assert.DoesNotContain(diagnostic.RelatedInformation ?? [], r => r.Message.Contains("refining module"));
  }

  public RefinementTests(ITestOutputHelper output, LogLevel dafnyLogLevel = LogLevel.Information) : base(output, dafnyLogLevel) {
  }
}