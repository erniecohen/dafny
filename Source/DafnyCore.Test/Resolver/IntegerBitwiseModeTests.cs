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
}
