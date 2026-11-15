using System.CommandLine;
using System.Reflection;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using Microsoft.Dafny;
using Xunit;

namespace DafnyDriver.Test;

public class VerificationCacheOptionAuditTest {
  [Fact]
  public void EveryModernOptionAndProverInputHasAnExplicitClassification() {
    var inventory = new List<string>();
    foreach (var option in Commands(DafnyNewCli.RootCommand).SelectMany(c => c.Options).Distinct()) {
      inventory.Add(string.Join('\t', "cli", option.Name,
        string.Join(',', option.Aliases.Order(StringComparer.Ordinal)), option.GetType().ToString()));
    }
    foreach (var property in new[] { typeof(SMTLibOptions) }.Concat(typeof(SMTLibOptions).GetInterfaces())
               .SelectMany(t => t.GetProperties())) {
      inventory.Add(string.Join('\t', "prover", property.DeclaringType!.FullName + "." + property.Name,
        "", property.PropertyType.ToString()));
    }
    foreach (var field in typeof(SMTLibSolverOptions).GetFields(BindingFlags.Instance | BindingFlags.Public)) {
      inventory.Add(string.Join('\t', "solver-field", field.Name, "", field.FieldType.ToString()));
    }
    foreach (var property in typeof(SMTLibSolverOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
      inventory.Add(string.Join('\t', "solver-property", property.Name, "", property.PropertyType.ToString()));
    }

    var root = new DirectoryInfo(AppContext.BaseDirectory);
    const string relativePath = "docs/dev/design/241-option-audit.tsv";
    while (root != null && !File.Exists(Path.Combine(root.FullName, relativePath))) { root = root.Parent; }
    Assert.NotNull(root);
    var rows = File.ReadAllLines(Path.Combine(root!.FullName, relativePath))
      .Where(line => !line.StartsWith('#') && line.Length != 0).Select(line => line.Split('\t')).ToArray();
    var stages = new[] { "input", "execution", "current-report", "selection", "profile", "bypass", "command-setup" };
    Assert.All(rows, row => {
      Assert.Equal(6, row.Length);
      Assert.Contains(row[4], stages);
      Assert.False(string.IsNullOrWhiteSpace(row[5]));
    });
    Assert.Equal(rows.Select(row => string.Join('\t', row.Take(4))).Order(StringComparer.Ordinal),
      inventory.Order(StringComparer.Ordinal));
  }

  private static IEnumerable<Command> Commands(Command command) =>
    new[] { command }.Concat(command.Subcommands.SelectMany(Commands));
}
