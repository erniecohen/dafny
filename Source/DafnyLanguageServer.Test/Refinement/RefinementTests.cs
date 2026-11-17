using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Extensions;
using Microsoft.Dafny.LanguageServer.IntegrationTest.Util;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
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

  // https://github.com/erniecohen/dafny/issues/411: a refining module that adds a field subject to definite
  // assignment verifies the constructor it inherits unchanged, which cannot assign the field.
  [Fact]
  public async Task AddedFieldChecksInheritedConstructor() {
    var source = @"
module A {
  class C { constructor () { } }
}
module B refines A {
  type Empty = x: int | false witness *
  class C ... { var e: Empty }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    var diagnostic = Assert.Single(diagnostics);
    Assert.Contains("field 'e'", diagnostic.Message);
    Assert.Equal(1, diagnostic.Range.Start.Line);
    Assert.Contains(diagnostic.RelatedInformation ?? [], r => r.Message.Contains("refining module"));
  }

  // https://github.com/erniecohen/dafny/issues/423: B checks the override of the member it inherits, which has the
  // position of C.L in A, so the two share one node of the verification tree.  Each reports its implementations to
  // that node, and the gutter shows the results of both, whichever reports last.  The order depends on scheduling, so
  // each test runs both ways: verifying each member once it is prepared, and preparing all members first, as the
  // gutter tests do (ProjectManager.GutterIconTesting).

  // A's member verifies; B's check fails.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GutterShowsTheInheritedOverrideCheck(bool prepareAllFirst) {
    PrepareAllFirst(prepareAllFirst);
    var source = @"
module A {
  trait S {
    lemma L() ensures true
  }
  class C extends S {
    lemma L() ensures true {
    }
  }
}
module B refines A {
  trait S ... {
    lemma L... ensures false
  }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    var diagnostic = Assert.Single(await GetLastDiagnostics(document, DiagnosticSeverity.Error));
    Assert.Equal(5, diagnostic.Range.Start.Line);
    var gutter = await SettledGutter(document);
    Assert.Equal(LineVerificationStatus.AssertionFailed, gutter[5]);
    await AssertVerificationHoverMatches(document, (5, 10), "???more detailed postcondition???");

    // Once B no longer changes S, it does not check the override again, and the node drops that check's result.
    ApplyChange(ref document, new Range(10, 0, 13, 0), "");
    Assert.Empty(await GetLastDiagnostics(document, DiagnosticSeverity.Error));
    gutter = await SettledGutter(document);
    Assert.Equal(LineVerificationStatus.Verified, gutter[5]);
  }

  // A's member fails its assertion; B's check passes or fails.
  [Theory]
  [InlineData(false, "1 == 1")]
  [InlineData(true, "1 == 1")]
  [InlineData(false, "false")]
  [InlineData(true, "false")]
  public async Task GutterKeepsTheBaseMembersFailure(bool prepareAllFirst, string refinedEnsures) {
    PrepareAllFirst(prepareAllFirst);
    var source = @"
module A {
  trait S {
    lemma L() ensures true
  }
  class C extends S {
    lemma L() ensures true {
      assert 1 == 2;
    }
  }
}
module B refines A {
  trait S ... {
    lemma L... ensures REFINED
  }
}".TrimStart().Replace("REFINED", refinedEnsures);
    var document = await CreateOpenAndWaitForResolve(source);
    var copyFails = refinedEnsures == "false";
    var diagnostics = await GetLastDiagnostics(document, DiagnosticSeverity.Error);
    Assert.Equal(copyFails ? 2 : 1, diagnostics.Length);
    var gutter = await SettledGutter(document);
    Assert.Equal(LineVerificationStatus.AssertionFailed, gutter[6]);
    if (copyFails) {
      Assert.Equal(LineVerificationStatus.AssertionFailed, gutter[5]);
    }
  }

  private void PrepareAllFirst(bool prepareAllFirst) {
    if (prepareAllFirst) {
      ProjectManager.GutterIconTesting = true;
      Disposable.Add(System.Reactive.Disposables.Disposable.Create(() => ProjectManager.GutterIconTesting = false));
    }
  }

  // The gutter icons of the first state in which no line is scheduled, verifying or obsolete.  Those statuses end in
  // 1 or 2; the settled ones end in 0.
  private async Task<LineVerificationStatus[]> SettledGutter(TextDocumentItem document) {
    await WaitUntilAllStatusAreCompleted(document);
    var manager = await Projects.GetProjectManager(document.Uri);
    return await manager!.States.Select(state => Gutter(state, document.Uri))
      .Where(gutter => gutter != null && gutter.All(line => (int)line % 10 == 0))
      .FirstAsync().ToTask(CancellationToken);
  }

  private static LineVerificationStatus[] Gutter(IdeState state, DocumentUri uri) {
    var tree = state.VerificationTrees.GetValueOrDefault(uri.ToUri());
    return tree == null ? null : VerificationStatusGutter.ComputeFrom(uri, null, tree.Children.ToList(),
      new Container<Diagnostic>(), tree.Range.End.Line + 1, true).PerLineStatus;
  }

  // https://github.com/erniecohen/dafny/issues/424: the warnings computed once an implementation has verified, such as
  // --warn-redundant-assumptions, belong to the member or the copy whose implementation it is.  At each resolution,
  // the member and B's copy of it, which share a range, carry over only their own warnings, so an edit that does not
  // verify again leaves the base member's warning single.
  [Fact]
  public async Task CopyDoesNotDuplicateTheBaseMembersWarnings() {
    await SetUp(options => {
      options.Set(CommonOptionBag.WarnRedundantAssumptions, true);
      options.Set(ProjectManager.Verification, VerifyOnMode.Save);
    });
    var source = @"
module A {
  trait S { lemma L(n: int) requires n > 4 ensures true }
  class C extends S { lemma L(n: int) requires n > 4 ensures true { } }
}
module B refines A {
  trait S ... { lemma L... ensures n > 0 }
}".TrimStart();
    var document = await CreateOpenAndWaitForResolve(source);
    await client.SaveDocumentAndWaitAsync(document, CancellationToken);
    AssertTheRedundantRequires(await GetLastDiagnostics(document));
    ApplyChange(ref document, new Range(6, 1, 6, 1), "\n// edit\n");
    AssertTheRedundantRequires(await GetLastDiagnostics(document, allowStale: true));
    ApplyChange(ref document, new Range(8, 0, 8, 0), "// another edit\n");
    AssertTheRedundantRequires(await GetLastDiagnostics(document, allowStale: true));
    await client.SaveDocumentAndWaitAsync(document, CancellationToken);
    AssertTheRedundantRequires(await GetLastDiagnostics(document));
  }

  private static void AssertTheRedundantRequires(Diagnostic[] diagnostics) {
    var diagnostic = Assert.Single(diagnostics);
    Assert.Contains("unnecessary requires clause", diagnostic.Message);
    Assert.Equal(2, diagnostic.Range.Start.Line);
  }

  /// <summary>
  /// The copy of C.L that B verifies has C.L's range.  The copy is scheduled before C.L, and its check passes while
  /// C.L's fails, so the range must not read Correct before C.L's tasks are identified (issue 433).  With the roles
  /// swapped, the range must not read Correct from C.L's tasks alone, should C.L be scheduled first.  C comes before
  /// S, so that no other member is scheduled before C.L, which would have A or B translated earlier.
  /// </summary>
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task SharedRangeIsNotCorrectUntilEachVerifiableIsPrepared(bool baseFails) {
    var source = $@"
module A {{
  class C extends S {{ lemma L() ensures true {{ {(baseFails ? "assert false;" : "")} }} }}
  trait S {{ lemma L() ensures true }}
}}
module B refines A {{
  trait S ... {{ lemma L... ensures {(baseFails ? "1 == 1" : "false")} }}
}}".TrimStart();
    await CreateOpenAndWaitForResolve(source);
    var statuses = new List<PublishedVerificationStatus>();
    while (statuses.Count == 0 || statuses[^1] != PublishedVerificationStatus.Error) {
      var fileStatus = await verificationStatusReceiver.AwaitNextNotificationAsync(CancellationToken);
      var status = fileStatus.NamedVerifiables.FirstOrDefault(n => n.NameRange.Start.Line == 1)?.Status;
      if (status != null && (statuses.Count == 0 || statuses[^1] != status)) {
        statuses.Add(status.Value);
      }
    }
    Assert.DoesNotContain(PublishedVerificationStatus.Correct, statuses);
  }

  public RefinementTests(ITestOutputHelper output, LogLevel dafnyLogLevel = LogLevel.Information) : base(output, dafnyLogLevel) {
  }
}