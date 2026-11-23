using System.CommandLine;
using System.Text;
using System.Text.Json;
using Microsoft.Dafny;
using Xunit.Abstractions;

namespace DafnyDriver.Test;

/// <summary>
/// Default-deny flag sensitivity of the persistent tables of --persistent-translation (issue 261).
/// Every registered option is classified, per stage, in docs/dev/design/261-option-classes.tsv as
/// one that the stage's key must distinguish (miss) or one it must not (hit, with a reason); an
/// option that verify does not accept is n/a. Each option of verify is run in process with two
/// values, and the stage digests that verify reports to its pipeline profile must differ exactly
/// for the stages classified miss. Boogie flags, passed through --boogie, have rows of their own.
/// </summary>
public class PersistentTranslationOptionClassesTest(ITestOutputHelper output) {
  private const string RelativePath = "docs/dev/design/261-option-classes.tsv";
  private static readonly string[] Stages = ["resolution", "translation", "results"];

  private sealed record Row(string Option, string[] Classes, string Sample, string Reason);

  private static string Root() {
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root != null && !File.Exists(Path.Combine(root.FullName, RelativePath))) { root = root.Parent; }
    Assert.NotNull(root);
    return root!.FullName;
  }

  private static Row[] Rows() => File.ReadAllLines(Path.Combine(Root(), RelativePath))
    .Where(line => !line.StartsWith('#') && line.Length != 0).Select(line => line.Split('\t')).Select(cells => {
      Assert.True(cells.Length == 6, string.Join('|', cells));
      return new Row(cells[0], cells[1..4], cells[4], cells[5]);
    }).ToArray();

  private static Command Verify => DafnyNewCli.RootCommand.Subcommands.Single(c => c.Name == "verify");

  private static IEnumerable<Command> Commands(Command command) =>
    new[] { command }.Concat(command.Subcommands.SelectMany(Commands));

  private static IEnumerable<Option> AllOptions =>
    Commands(DafnyNewCli.RootCommand).SelectMany(c => c.Options).DistinctBy(o => o.Name);

  [Fact]
  public void EveryOptionIsClassifiedForEveryStage() {
    var rows = Rows();
    var verify = Verify.Options.Select(o => o.Name).ToHashSet();
    var options = rows.Where(r => !r.Option.StartsWith("boogie:")).Select(r => r.Option).ToArray();
    Assert.Equal(options.Length, options.Distinct().Count());
    // Default-deny: an option this table does not name fails here, before any key can ignore it.
    Assert.Equal(AllOptions.Select(o => o.Name).Order(StringComparer.Ordinal), options.Order(StringComparer.Ordinal));
    foreach (var row in rows) {
      var notVerify = row.Classes.All(c => c == "n/a");
      Assert.True(notVerify == (!row.Option.StartsWith("boogie:") && !verify.Contains(row.Option)), row.Option);
      Assert.All(row.Classes, c => Assert.Contains(c, new[] { "miss", "hit", "n/a" }));
      Assert.True(row.Classes.All(c => c == "miss") || row.Reason.Length != 0, $"{row.Option}: a stage that ignores an option needs a reason");
      Assert.True(row.Sample.Length != 0, row.Option);
    }
    // The flags of --boogie whose effect the persistent tables are known to depend on.
    foreach (var flag in new[] { "/normalizeDeclarationOrder", "/structuralLiterals", "/cacheUsedTypes" }) {
      Assert.Contains(rows, r => r.Option == "boogie:" + flag);
    }
  }

  [Fact]
  public async Task EachStageMissesExactlyOnTheOptionsItIsSensitiveTo() {
    var directory = Directory.CreateTempSubdirectory("dafny-option-classes");
    try {
      var input = Path.Combine(directory.FullName, "input.dfy");
      // M has something to verify, so that its translation is looked up.
      await File.WriteAllTextAsync(input, "module M { lemma L() ensures true { } }\n");
      foreach (var name in new[] { "a", "b" }) {
        await File.WriteAllTextAsync(Path.Combine(directory.FullName, name + ".dfy"), "module L { }\n");
      }
      // Two preludes that differ only in a comment, both copies of the one this build ships.
      var shipped = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl"));
      await File.WriteAllTextAsync(Path.Combine(directory.FullName, "prelude-a.bpl"), shipped);
      await File.WriteAllTextAsync(Path.Combine(directory.FullName, "prelude-b.bpl"), shipped + "\n// another prelude\n");
      var run = 0;
      // The pinned solver, when it is present, so that the module-results environment is identified.
      var solver = Path.Combine(Root(), "Binaries", "z3", "bin", "z3-5.1.0");
      var solverArgs = File.Exists(solver) ? new[] { "--solver-path", solver } : [];
      // The digests of the options alone that verify reports before it resolves anything, and the
      // keys module M is then looked up under (null when the module is translated afresh).
      async Task<(Dictionary<string, string> Model, Dictionary<string, string?>? Keys)?> Digests(IEnumerable<string> extra) {
        var profile = Path.Combine(directory.FullName, $"profile{run++}.jsonl");
        var args = new[] { "verify", input, "--type-system-refresh", "--persistent-translation",
          "--verification-cache", Path.Combine(directory.FullName, "cache"), "--pipeline-profile", profile }.Concat(solverArgs).Concat(extra).ToArray();
        var writer = new StringWriter();
        await File.AppendAllTextAsync(Path.Combine(directory.FullName, "runs.log"), string.Join(' ', args) + "\n");
        var running = DafnyBackwardsCompatibleCli.MainWithWriters(writer, writer, TextReader.Null, args);
        // The digests are reported before anything is verified; an option that waits (for a
        // debugger, say) reports nothing.
        await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(60)));
        if (running.IsCompleted && File.Exists(profile)) {
          Dictionary<string, string>? model = null;
          Dictionary<string, string?>? keys = null;
          foreach (var line in await File.ReadAllLinesAsync(profile)) {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            switch (root.GetProperty("ev").GetString()) {
              case "persistent-options":
                model = Stages.ToDictionary(s => s, s => root.GetProperty(s).GetString()!);
                break;
              case "persistent-keys" when root.GetProperty("module").GetString() == "M":
                keys = Stages.ToDictionary(s => s, s => root.GetProperty(s).GetString());
                break;
            }
          }
          if (model != null) { return (model, keys); }
        }
        output.WriteLine(string.Join(' ', args) + "\n" + writer);
        return null;
      }

      var observedKeys = 0;
      var disagreements = new StringBuilder();
      async Task<string[]?> Observe(string option, string sample) {
        var values = sample.Replace("{dir}", directory.FullName).Split(" | ");
        Assert.True(values.Length == 2, option);
        var name = option.StartsWith("boogie:") ? "--boogie" : "--" + option;
        // "(none)" is the option left out; a value that starts with -- is a list of arguments.
        IEnumerable<string> Args(string value) => value == "(none)" ? [] : value.StartsWith("--") ? value.Split(' ') :
          name == "--boogie" ? [name, value] : [name + ":" + value];
        var first = await Digests(Args(values[0]));
        var second = await Digests(Args(values[1]));
        if (first is not { } a || second is not { } b) { return null; }
        var model = Stages.Select(s => a.Model[s] != b.Model[s] ? "miss" : "hit").ToArray();
        if (a.Keys == null || b.Keys == null) { return model; }
        // The keys the tables are actually looked up under must agree with the digests of the
        // options alone, so that the classification checks the real keys and not only a model. A
        // stage without a key in either run (results, when the environment is not identified) is
        // checked by its digest alone.
        observedKeys++;
        var keys = Stages.Select((s, i) => a.Keys[s] == null || b.Keys[s] == null ? model[i]
          : a.Keys[s] != b.Keys[s] ? "miss" : "hit").ToArray();
        // The results key also includes the task-result cache's frozen solver options, which the
        // digest of the options alone leaves out, so that stage is checked by its key alone.
        for (var i = 0; i < Stages.Length; i++) {
          if (keys[i] != model[i] && Stages[i] != "results") {
            disagreements.AppendLine($"{option}: the {Stages[i]} key {(keys[i] == "miss" ? "differs" : "does not differ")} for {sample}, but the digest of its options {(model[i] == "miss" ? "does" : "does not")}");
          }
        }
        return keys;
      }

      // With this variable set, the test writes what it observes for every option, with samples
      // derived from each option's type, as a starting point for the table.
      if (Environment.GetEnvironmentVariable("DAFNY_OBSERVE_OPTION_CLASSES") is { Length: > 0 } observed) {
        var verify = Verify.Options.Select(o => o.Name).ToHashSet();
        var skip = (Environment.GetEnvironmentVariable("DAFNY_OBSERVE_OPTION_SKIP") ?? "").Split(',').ToHashSet();
        await File.WriteAllTextAsync(observed, "");
        foreach (var option in AllOptions.OrderBy(o => o.Name, StringComparer.Ordinal)) {
          string line;
          if (!verify.Contains(option.Name)) {
            line = $"{option.Name}\tn/a\tn/a\tn/a\t-\tnot an option of verify";
          } else {
            var sample = skip.Contains(option.Name) ? null : Sample(option.ValueType);
            await File.AppendAllTextAsync(observed, "# observing " + option.Name + "\n");
            var classes = sample == null ? null : await Observe(option.Name, sample);
            line = $"{option.Name}\t{string.Join('\t', classes ?? ["?", "?", "?"])}\t{sample ?? "?"}\t";
          }
          await File.AppendAllTextAsync(observed, line + "\n");
        }
        return;
      }

      var failures = new StringBuilder();
      foreach (var row in Rows().Where(r => r.Classes.Any(c => c != "n/a") && r.Sample != "fixed")) {
        var classes = await Observe(row.Option, row.Sample);
        if (classes == null) { failures.AppendLine($"{row.Option}: verify did not report its option digests"); continue; }
        for (var i = 0; i < Stages.Length; i++) {
          if (classes[i] != row.Classes[i]) {
            failures.AppendLine($"{row.Option}: {Stages[i]} is classified {row.Classes[i]} but its key {(classes[i] == "miss" ? "differs" : "does not differ")} for {row.Sample}");
          }
        }
      }
      failures.Append(disagreements);
      Assert.True(failures.Length == 0, failures.ToString());
      output.WriteLine($"{observedKeys} options were checked against the keys module M is looked up under");
      Assert.True(observedKeys > 100, $"only {observedKeys} options were checked against the real keys");
    } finally {
      directory.Delete(true);
    }
  }

  private static string? Sample(System.Type type) {
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type == typeof(bool)) { return "false | true"; }
    if (type == typeof(int) || type == typeof(uint)) { return "1 | 2"; }
    if (type.IsEnum) { return string.Join(" | ", Enum.GetNames(type).Take(2)); }
    if (type == typeof(string) || type == typeof(FileInfo) || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) {
      return "{dir}/a.dfy | {dir}/b.dfy";
    }
    return null;
  }
}
