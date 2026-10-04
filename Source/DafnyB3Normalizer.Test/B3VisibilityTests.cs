// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3VisibilityTests {
  [Fact]
  public void OwnedAnalysisRetainsNamedWildcardAndScopeMasksWithoutMutation() {
    var (program, options) = Parse("""
      function F(): int;
      procedure P();
      implementation P() {
        hide *; assert true;
        push; reveal F; assert true; pop;
        assert true;
        reveal *; assert true;
      }
      """);
    var implementation = program.Implementations.Single();
    var emitted = Emit(program, options);
    var predecessors = implementation.Blocks.Select(block => block.Predecessors.ToArray()).ToArray();
    var analysis = new B3DefinitionVisibility(implementation);
    var function = program.Functions.Single();
    var checks = implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().ToArray();
    Assert.Equal(4, checks.Length);
    Assert.False(analysis.After(checks[0]).IsRevealed(function));
    Assert.True(analysis.After(checks[1]).IsRevealed(function));
    Assert.False(analysis.After(checks[2]).IsRevealed(function));
    Assert.True(analysis.After(checks[3]).IsRevealed(function));
    Assert.Equal(emitted, Emit(program, options));
    Assert.All(Enumerable.Range(0, predecessors.Length), i =>
      Assert.Equal(predecessors[i], implementation.Blocks[i].Predecessors));
  }

  [Fact]
  public void ChangedBackedgeFailsBeforeAssigningAnInvariantMask() {
    var (program, _) = Parse("""
      function F(): int;
      procedure P(); implementation P() {
        while (*) invariant true; { hide F; }
      }
      """);
    var rejection = Assert.Throws<B3DefinitionVisibility.Rejection>(() =>
      new B3DefinitionVisibility(program.Implementations.Single()));
    Assert.Contains("cycle", rejection.Message);
  }

  [Fact]
  public void StableVisibilityLoopsKeepTheExistingInductionBoundary() {
    var (program, _) = Parse("""
      function F(): int;
      procedure P(); implementation P() {
        hide F; while (*) invariant true; { push; assert true; pop; }
      }
      """);
    var implementation = program.Implementations.Single();
    var analysis = new B3DefinitionVisibility(implementation);
    Assert.All(implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>(),
      check => Assert.False(analysis.After(check).IsRevealed(program.Functions.Single())));
  }

  [Fact]
  public void EqualTopFramesCannotHideDifferentOuterScopeFramesAtAJoin() {
    var (source, _) = Parse("""
      function F(): int; procedure P(); implementation P() {
        hide *;
        if (*) { reveal F; push; hide *; } else { push; hide *; }
        assert true; pop;
      }
      """);
    var rejection = Assert.Throws<B3DefinitionVisibility.Rejection>(() => new B3DefinitionVisibility(source.Implementations.Single()));
    Assert.Contains("outer scope stacks", rejection.Message);
  }

  [Fact]
  public void MustAvailabilityDoesNotReplaceThePinnedMixedModeMerge() {
    var (source, _) = Parse("function F(): int; procedure P(); implementation P() { if (*) { reveal *; } else { hide *; } assert F() == 7; }");
    var implementation = source.Implementations.Single(); var function = source.Functions.Single();
    var assertion = implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Single();
    var exact = new B3DefinitionVisibility(implementation);
    Assert.True(exact.Before(assertion).IsRevealed(function));
    var must = exact.AnalyzeMust(new[] { function }); var state = must.Before(assertion);
    Assert.False(state.IsRevealed(function)); Assert.False(state.AllReveal); Assert.True(state.MayReveal);
  }

  [Fact]
  public void MustVisibilityRestoresTheWholeSavedScopeFrame() {
    var (source, _) = Parse("function F(): int; procedure P(); implementation P() { hide *; push; reveal F; assert true; pop; assert true; }");
    var implementation = source.Implementations.Single(); var function = source.Functions.Single();
    var assertions = implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().ToArray();
    var must = new B3DefinitionVisibility(implementation).AnalyzeMust(new[] { function });
    Assert.True(must.Before(assertions[0]).IsRevealed(function)); Assert.False(must.Before(assertions[0]).MayReveal);
    Assert.False(must.Before(assertions[1]).IsRevealed(function)); Assert.False(must.Before(assertions[1]).MayReveal);
  }

  [Fact]
  public void EntryReachabilityDoesNotSeedDisconnectedTrailingPopsOrAssertions() {
    var (source, _) = Parse("function F(): int; procedure P(); implementation P() { push; assert true; pop; return; pop; assert false; }");
    var implementation = source.Implementations.Single(); var owner = source.Functions.Single();
    var assertions = implementation.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().ToArray();
    Assert.Equal(2, assertions.Length);
    var blocks = implementation.Blocks.ToArray();
    var analysis = new B3DefinitionVisibility(implementation); var must = analysis.AnalyzeMust(new[] { owner });
    Assert.True(analysis.IsReachable(assertions[0])); Assert.False(analysis.IsReachable(assertions[1]));
    Assert.True(must.Before(assertions[0]).IsRevealed(owner));
    Assert.False(must.Before(assertions[1]).IsRevealed(owner)); Assert.False(must.Before(assertions[1]).MayReveal);
    Assert.Equal(blocks, implementation.Blocks);
    Assert.Equal(2, must.NativeAssertionOperands().Count);
    Assert.Throws<B3DefinitionVisibility.Rejection>(() => analysis.IsReachable(new Bpl.AssertCmd(Bpl.Token.NoToken, Bpl.Expr.False)));
  }

  [Fact]
  public void ReachableUnmatchedPopRemainsUnsupported() {
    var (source, _) = Parse("procedure P(); implementation P() { pop; assert false; }");
    var rejection = Assert.Throws<B3DefinitionVisibility.Rejection>(() => new B3DefinitionVisibility(source.Implementations.Single()));
    Assert.Contains("pop has no matching push", rejection.Message);
  }

  internal static (Bpl.Program Program, DafnyOptions Options) Parse(string text) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    Assert.Equal(0, Bpl.Parser.Parse(text, "B3VisibilityTests.bpl", out var program));
    Assert.Equal(0, program.Resolve(options)); Assert.Equal(0, program.Typecheck(options));
    return (program, options);
  }
  private static string Emit(Bpl.Program program, DafnyOptions options) {
    using var output = new StringWriter(); using var writer = new Bpl.TokenTextWriter(output, options);
    program.Emit(writer); return output.ToString();
  }
}
