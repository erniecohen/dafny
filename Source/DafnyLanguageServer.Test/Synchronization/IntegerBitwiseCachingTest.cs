// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public class IntegerBitwiseCachingTest {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void IdenticalSourceRetainsItsCurrentRecordedMode(bool firstMode) {
    var parser = new ExposedCachingParser();
    DfyParseFileResult Parse(bool enabled) {
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
      options.ApplyDefaultOptionsWithoutSettingsDefault();
      options.Set(CommonOptionBag.IntegerBitwise, enabled);
      return parser.Parse(options);
    }

    void HasMode(DfyParseFileResult result, bool enabled) {
      Assert.Equal(0, result.ErrorReporter.ErrorCount);
      Assert.Equal(enabled, result.ErrorReporter.Options.Get(CommonOptionBag.IntegerBitwise));
      var module = Assert.Single(result.Module.SourceDecls.OfType<LiteralModuleDecl>());
      Assert.Equal(enabled, module.Options.Get(CommonOptionBag.IntegerBitwise));
      var function = Assert.Single(module.ModuleDef.TopLevelDecls.OfType<DefaultClassDecl>()).Members.OfType<Function>().Single();
      Assert.Equal(enabled, function.Body is IntegerBitwiseSyntaxExpr);
    }

    var first = Parse(firstMode);
    HasMode(first, firstMode);
    var same = Parse(firstMode);
    HasMode(same, firstMode);
    Assert.Same(first.ErrorReporter, same.ErrorReporter); // genuine cache hit

    var changed = Parse(!firstMode);
    HasMode(changed, !firstMode);
    Assert.NotSame(first.ErrorReporter, changed.ErrorReporter);
    var changedAgain = Parse(!firstMode);
    HasMode(changedAgain, !firstMode);
    Assert.Same(changed.ErrorReporter, changedAgain.ErrorReporter);

    var restored = Parse(firstMode);
    HasMode(restored, firstMode);
    Assert.Same(first.ErrorReporter, restored.ErrorReporter);
  }

  private sealed class ExposedCachingParser() : CachingParser(NullLogger<ProgramParser>.Instance,
    OnDiskFileSystem.Instance, new NoTelemetryPublisher()) {
    internal DfyParseFileResult Parse(DafnyOptions options) => base.ParseFile(options,
      new FileSnapshot(new StringReader("module Library { newtype {:bitWidth 8} U = x:nat | x < 256 ghost function F(a:U,b:U):U { a & b } }"), null),
      new Uri("untitled:integer-bitwise-mode.dfy"), CancellationToken.None);
  }

  private sealed class NoTelemetryPublisher() : TelemetryPublisherBase(NullLogger<TelemetryPublisherBase>.Instance) {
    public override void PublishTelemetry(ImmutableDictionary<string, object> data) { }
  }
}
