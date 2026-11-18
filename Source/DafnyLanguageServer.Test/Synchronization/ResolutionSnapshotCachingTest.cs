#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public class ResolutionSnapshotCachingTest {
  private sealed class NoTelemetryPublisher() : TelemetryPublisherBase(NullLogger<TelemetryPublisherBase>.Instance) {
    public override void PublishTelemetry(ImmutableDictionary<string, object> data) { }
  }

  [Fact]
  public async Task DetachedProfileMaterializesCurrentGraphsDespiteStableParseIdentities() {
    const string source = "module Base { const N: int := 1 } module Client { import B = Base }";
    var directory = Path.Combine(Path.GetTempPath(), "snapshot-editor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
      options.ApplyDefaultOptionsWithoutSettingsDefault();
      options.Set(CommonOptionBag.TypeSystemRefresh, true);
      options.Set(CommonOptionBag.ResolutionCache, Path.Combine(directory, "cache"));
      var uri = new Uri("untitled:snapshot-editor.dfy");
      var template = await ProgramParser.Parse(source, uri, new BatchErrorReporter(options));
      var cache = new ResolutionCache();
      Program? previous = null;
      foreach (var round in Enumerable.Range(0, 3)) {
        var profile = Path.Combine(directory, round + ".jsonl");
        options.Set(CommonOptionBag.PipelineProfile, profile);
        var parsed = await ProgramParser.Parse(source, uri, new BatchErrorReporter(options));
        var program = new Program(new Cloner(true), template.Program) {
          Reporter = parsed.Program.Reporter, SystemModuleManager = parsed.Program.SystemModuleManager
        };
        await new CachingResolver(program, NullLogger<CachingResolver>.Instance, new NoTelemetryPublisher(), cache).Resolve(CancellationToken.None);
        PipelineProfile.For(options)!.Finish("test");
        Assert.Equal(0, program.Reporter.ErrorCount);
        var events = File.ReadLines(profile).Select(s => JsonDocument.Parse(s).RootElement.Clone())
          .Where(e => e.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.snapshot").ToArray();
        var aliasEvent = events.Single(e => e.GetProperty("module").GetString() == "Client.B");
        Assert.Equal(round == 0 ? "miss" : "hit", aliasEvent.GetProperty("state").GetString());
        var target = program.DefaultModuleDef.SourceDecls.OfType<LiteralModuleDecl>().Single(d => d.Name == "Base");
        var client = program.DefaultModuleDef.SourceDecls.OfType<LiteralModuleDecl>().Single(d => d.Name == "Client");
        var alias = client.ModuleDef.SourceDecls.OfType<AliasModuleDecl>().Single();
        Assert.Same(target, alias.TargetQId.Decl);
        Assert.Same(target.Signature, alias.Signature);
        if (previous != null) {
          var old = previous.DefaultModuleDef.SourceDecls.OfType<LiteralModuleDecl>().Single(d => d.Name == "Base");
          Assert.Equal(old.CloneId, target.CloneId);
          Assert.NotSame(old, target);
          Assert.NotSame(old.Signature, alias.Signature);
        }
        previous = program;
      }
    } finally { Directory.Delete(directory, true); }
  }
}
