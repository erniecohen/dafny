// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.Dafny;

namespace DafnyCore.Test.Resolver.Cardinality;

[Collection("Cardinality resolution")]
public class CardinalityParentTests {
  [Theory]
  [InlineData("X", "Y", true)]
  [InlineData("Y", "X", true)]
  [InlineData("X", "int", false)]
  [InlineData("seq<X>", "Y", false)]
  [InlineData("Id<X>", "Y", true)]
  [InlineData("Sub<X>", "Y", false)]
  public async Task RetentionIsDirectPositionalAndExpandsOnlyIdentityAliases(
    string first, string second, bool accepted) {
    var (program, _) = await CardinalitySourceTests.ResolveAsync($$"""
      trait Parent<A, B> {}
      type Id<T> = T
      type Sub<T> = x: T | true witness *
      datatype Child<X, Y> = Child(x: X, y: Y)
      datatype Uses<X, Y> = Uses(first: {{first}}, second: {{second}})
      """);
    var child = CardinalityProfileTests.Find(program, "Uses");
    var actuals = ((DatatypeDecl)child).Ctors.Single().Formals.Select(formal => new CardinalityTypeUse(formal.Type)).ToImmutableArray();
    var parent = CardinalityProfileTests.Find(program, "Parent");
    var reporter = new BatchErrorReporter(program.Options);
    var info = Info(child);
    var instance = new CardinalityParentInstance(parent, actuals, CardinalityProfileTests.Reason(child));
    var checker = new CardinalityParentChecker(CardinalityProfileTests.Visitor(program), reporter, CancellationToken.None);
    Assert.Equal(accepted, checker.Check(info, instance));
    Assert.Equal(accepted, reporter.ErrorCount == 0);
    if (!accepted) {
      Assert.All(reporter.AllMessagesByLevel[ErrorLevel.Error], diagnostic =>
        Assert.Equal("r_cardinality_unretained_parameter", diagnostic.ErrorId));
    }
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, true)]
  public async Task DuplicateRetainingSlotsChooseASufficientContract(bool secondPermissive, bool accepted) {
    var (program, _) = await CardinalitySourceTests.ResolveAsync(
      $"trait Parent<A, {(secondPermissive ? "!" : "")}B> {{}} datatype Child<!X> = Child(p: X -> bool)");
    var child = CardinalityProfileTests.Find(program, "Child");
    var parent = CardinalityProfileTests.Find(program, "Parent");
    var actual = new CardinalityTypeUse(new UserDefinedType(child.TypeArgs.Single()));
    var reporter = new BatchErrorReporter(program.Options);
    var instance = new CardinalityParentInstance(parent, [actual, actual], CardinalityProfileTests.Reason(child));
    var checker = new CardinalityParentChecker(CardinalityProfileTests.Visitor(program), reporter, CancellationToken.None);
    Assert.Equal(accepted, checker.Check(Info(child), instance));
    if (!accepted) {
      Assert.Equal("r_cardinality_parent_contract", Assert.Single(reporter.AllMessagesByLevel[ErrorLevel.Error]).ErrorId);
    }
  }

  [Fact]
  public async Task ReferenceParentAllowsAbsentFormalsButChecksExposedModes() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync(
      "trait Parent<A> extends object {} trait Empty extends object {} datatype Child<!X> = Child(p: X -> bool)");
    var child = CardinalityProfileTests.Find(program, "Child");
    var parent = CardinalityProfileTests.Find(program, "Parent");
    var empty = CardinalityProfileTests.Find(program, "Empty");
    var reporter = new BatchErrorReporter(program.Options);
    var checker = new CardinalityParentChecker(CardinalityProfileTests.Visitor(program), reporter, CancellationToken.None);
    Assert.True(checker.Check(Info(child), new CardinalityParentInstance(empty, [], CardinalityProfileTests.Reason(child))));
    var formal = new CardinalityTypeUse(new UserDefinedType(child.TypeArgs.Single()));
    Assert.False(checker.Check(Info(child), new CardinalityParentInstance(parent, [formal], CardinalityProfileTests.Reason(child))));
    Assert.Equal("r_cardinality_parent_contract", Assert.Single(reporter.AllMessagesByLevel[ErrorLevel.Error]).ErrorId);
  }

  [Fact]
  public async Task InvalidParentArityFailsClosed() {
    var (program, _) = await CardinalitySourceTests.ResolveAsync("trait Parent<A> {} datatype Child = Child");
    var child = CardinalityProfileTests.Find(program, "Child");
    var parent = CardinalityProfileTests.Find(program, "Parent");
    var checker = new CardinalityParentChecker(CardinalityProfileTests.Visitor(program),
      new BatchErrorReporter(program.Options), CancellationToken.None);
    Assert.Throws<CardinalityTypeException>(() => checker.Check(Info(child),
      new CardinalityParentInstance(parent, [], CardinalityProfileTests.Reason(child))));
  }

  private static CardinalityDeclarationInfo Info(TopLevelDecl declaration) =>
    new(declaration, declaration.TypeArgs.Select(CardinalityWeights.Mode).ToImmutableArray(), [], []);
}
