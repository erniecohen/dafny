// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using Microsoft.Dafny;
using DafnyType = Microsoft.Dafny.Type;

namespace DafnyCore.Test.Resolver;

[Collection("Obligation translation")]
public class IntegerBitwiseModeTests {
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task EveryResolutionEntryHonorsRecordedFileMode(bool recursive, bool recordedOff) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.IntegerBitwise, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.MemberInvariants, recursive);
    options.Set(CommonOptionBag.RecursiveModules, recursive);
    var reporter = new BatchErrorReporter(options);
    var uri = new Uri("untitled:integer-bitwise-mode.dfy");
    var source = recursive
      ? "module A { import B ghost function Observe():int { IntegerBitwiseModel.P(0) } } module B { import A }"
      : "ghost function Observe():int { IntegerBitwiseModel.P(0) }";
    var parsed = await ProgramParser.Parse(source, uri, reporter);
    Assert.Equal(0, reporter.ErrorCount);
    if (recordedOff) {
      // Seed the parser's provenance contract directly. This also exercises the
      // group entry independently of whether library groups can be serialized.
      parsed.Program.Compilation.IntegerBitwiseDisabledSources = new HashSet<Uri> { uri };
    }
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    if (recordedOff) {
      Assert.True(reporter.ErrorCount > 0);
      Assert.All(reporter.AllMessages.Where(diagnostic => diagnostic.Level == ErrorLevel.Error), diagnostic =>
        Assert.Contains("compiler-owned integer bitwise references require --integer-bitwise", diagnostic.Message));
    } else {
      Assert.Equal(0, reporter.ErrorCount);
    }
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, false)]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(true, true, true)]
  public async Task OperatorsHonorRecordedFileMode(bool refreshed, bool recursive, bool recordedOff) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.IntegerBitwise, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refreshed);
    options.Set(CommonOptionBag.MemberInvariants, recursive);
    options.Set(CommonOptionBag.RecursiveModules, recursive);
    var reporter = new BatchErrorReporter(options);
    var uri = new Uri("untitled:integer-bitwise-operators-mode.dfy");
    const string declarations = "newtype {:bitWidth 8} U = x:nat | x < 256 " +
      "ghost function Observe(a:U,b:U):U { a & b }";
    var source = recursive ? "module A { import B " + declarations + " } module B { import A }" : declarations;
    var parsed = await ProgramParser.Parse(source, uri, reporter);
    Assert.Equal(0, reporter.ErrorCount);
    if (recordedOff) {
      parsed.Program.Compilation.IntegerBitwiseDisabledSources = new HashSet<Uri> { uri };
    }
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Equal(recordedOff || recursive && !refreshed, reporter.ErrorCount > 0);
    if (recursive && !refreshed) {
      Assert.Contains(reporter.AllMessages, diagnostic => diagnostic.Message.Contains("type-system-refresh"));
    }
  }


  [Theory]
  [InlineData(false, false, false)]
  [InlineData(false, false, true)]
  [InlineData(false, true, false)]
  [InlineData(false, true, true)]
  [InlineData(true, false, false)]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(true, true, true)]
  public async Task SourceMembersHonorRecordedFileMode(bool refreshed, bool recursive, bool recordedOff) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.IntegerBitwise, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refreshed);
    options.Set(CommonOptionBag.MemberInvariants, recursive);
    options.Set(CommonOptionBag.RecursiveModules, recursive);
    var reporter = new BatchErrorReporter(options);
    var uri = new Uri("untitled:integer-bitwise-members-mode.dfy");
    const string declarations = "newtype {:bitWidth 8} U=x:nat | x<256 " +
      "ghost function Left(a:U,k:int):U { a.RotateLeft(k) } " +
      "ghost function Right(a:U,k:int):U { a.RotateRight(k) } " +
      "ghost function Bit(a:U,i:int):bool { a.bit(i) } " +
      "ghost function Population(a:U):nat { a.PopCount() }";
    var source = recursive ? "module A { import B " + declarations + " } module B { import A }" : declarations;
    var parsed = await ProgramParser.Parse(source, uri, reporter);
    Assert.Equal(0, reporter.ErrorCount);
    if (recordedOff) {
      parsed.Program.Compilation.IntegerBitwiseDisabledSources = new HashSet<Uri> { uri };
    }
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Equal(recordedOff || recursive && !refreshed, reporter.ErrorCount > 0);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PopulationResultPreservesNatAndReferenceIdentity(bool refreshed) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.IntegerBitwise, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refreshed);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(
      "newtype {:bitWidth 80} {:nativeType false} U=x:nat | 16<=x<256 witness 16 " +
      "function Count(a:U):nat { a.PopCount() }", new Uri("untitled:population-source.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Equal(0, reporter.ErrorCount);
    var function = parsed.Program.DefaultModuleDef.TopLevelDecls.OfType<DefaultClassDecl>()
      .SelectMany(declaration => declaration.Members).OfType<Function>().Single();
    var conversion = Assert.IsType<ConversionExpr>(function.Body.Resolved);
    Assert.Equal("nat", conversion.ToType.ToString());
    Assert.Equal(80, conversion.IntegerBitwiseWidth);
    var reference = Assert.IsType<FunctionCallExpr>(conversion.E.Resolved);
    Assert.Equal("popNat", reference.Function.Name);
    Assert.IsType<IntegerBitwiseReferences>(reference.Function.EnclosingClass);
    Assert.Single(conversion.IntegerBitwiseOperands);
    var clone = Assert.IsType<ConversionExpr>(new Cloner(cloneResolvedFields: true).CloneExpr(conversion));
    Assert.Equal(80, clone.IntegerBitwiseWidth);
    Assert.Equal("nat", clone.ToType.ToString());
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task IntegerComplementConstantFoldingDoesNotUseABitvectorCarrier(bool refreshed) {
    DafnyType.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.IntegerBitwise, true);
    options.Set(CommonOptionBag.TypeSystemRefresh, refreshed);
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(
      "newtype {:bitWidth 12} {:nativeType \"byte\"} U = x:nat | x < 256 " +
      "ghost function Observe(a:U):U { !a }", new Uri("untitled:integer-complement.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Equal(0, reporter.ErrorCount);
    var function = parsed.Program.DefaultModuleDef.TopLevelDecls.OfType<DefaultClassDecl>()
      .SelectMany(declaration => declaration.Members).OfType<Function>().Single();
    var unary = Assert.IsType<UnaryOpExpr>(Assert.IsType<IntegerBitwiseSyntaxExpr>(function.Body).Syntax);
    // Exercise folding of the temporary source node before it is replaced by
    // the reference call, with a literal carrying the resolved integer type.
    unary.E = Expression.CreateIntLiteralNonnegative(unary.Origin, 0, unary.Type);
    Assert.Null(ConstantFolder.TryFoldInteger(unary));
  }
}
