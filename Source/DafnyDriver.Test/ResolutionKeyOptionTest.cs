using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Dafny;
using Xunit;

namespace DafnyDriver.Test;

/// <summary>
/// The resolution key is default-deny over options (issue 261): every option of every command is in it unless
/// <see cref="ModuleResolutionKey.IgnoredOptions"/> lists it with a reason. Starting from every option at its
/// default, toggling one option must change the digest exactly when it is not listed.
/// </summary>
public class ResolutionKeyOptionTest {
  private static readonly IReadOnlyList<Option> AllOptions = Commands(DafnyNewCli.RootCommand)
    .SelectMany(c => c.Options).Distinct().Where(o => o.Name is not ("help" or "version")).ToList();

  [Fact]
  public void EveryIgnoredOptionIsARegisteredOptionWithAReason() {
    var names = AllOptions.Select(o => o.Name).ToHashSet();
    Assert.All(ModuleResolutionKey.IgnoredOptions, entry => {
      Assert.Contains(entry.Key, names);
      Assert.False(string.IsNullOrWhiteSpace(entry.Value));
    });
  }

  [Fact]
  public void TogglingAnOptionChangesTheKeyExactlyWhenItIsNotIgnored() {
    var baseline = Defaults();
    var expected = ModuleResolutionKey.OptionsDigest(baseline);
    var wrong = new List<string>();
    var toggled = 0;
    foreach (var option in AllOptions) {
      var options = Defaults();
      var current = options.Options.OptionArguments[option];
      var other = Toggle(option.ValueType, current);
      Assert.False(Equals(other, current), option.Name);
      options.Options.OptionArguments[option] = other!;
      toggled++;
      var changed = ModuleResolutionKey.OptionsDigest(options) != expected;
      if (changed == ModuleResolutionKey.IgnoredOptions.ContainsKey(option.Name)) {
        wrong.Add(option.Name + (changed ? ": ignored, yet changes the key" : ": not ignored, yet leaves the key"));
      }
    }
    Assert.True(toggled > 150, "the inventory must cover the commands' options: " + toggled);
    Assert.Empty(wrong);
    // Positive control: the digest is a function of the options, so the unchanged defaults give it again.
    Assert.Equal(expected, ModuleResolutionKey.OptionsDigest(Defaults()));
  }

  [Fact]
  public void ProgressDoesNotChangeTheKey() {
    var options = Defaults();
    var expected = ModuleResolutionKey.OptionsDigest(options);
    foreach (var level in Enum.GetValues<CommonOptionBag.ProgressLevel>()) {
      options.Options.OptionArguments[CommonOptionBag.ProgressOption] = level;
      Assert.Equal(expected, ModuleResolutionKey.OptionsDigest(options));
    }
  }

  /// <summary>Options holding every option's default, as a command line naming none of them gives.</summary>
  private static DafnyOptions Defaults() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    foreach (var option in AllOptions) {
      var command = new RootCommand { option };
      var result = command.Parse(Array.Empty<string>());
      var value = typeof(ParseResult).GetMethods()
        .Single(m => m.Name == nameof(ParseResult.GetValueForOption) && m.IsGenericMethod)
        .MakeGenericMethod(option.ValueType).Invoke(result, new object[] { option });
      options.Options.OptionArguments[option] = value!;
    }
    return options;
  }

  private static object? Toggle(System.Type type, object? value) {
    if (type == typeof(bool)) { return !(bool)value!; }
    if (type == typeof(int)) { return (int)value! + 1; }
    if (type == typeof(uint)) { return (uint)value! + 1; }
    if (type == typeof(string)) { return (string?)value + "x"; }
    if (type == typeof(FileInfo)) { return new FileInfo(((FileInfo?)value)?.FullName + "x"); }
    if (Nullable.GetUnderlyingType(type) is { IsEnum: true } underlying) {
      return value == null ? Enum.GetValues(underlying).GetValue(0) : null;
    }
    if (type.IsEnum) {
      return Enum.GetValues(type).Cast<object>().First(v => !Equals(v, value));
    }
    if (type == typeof(IEnumerable<string>) || type == typeof(IList<string>)) {
      return (((IEnumerable<string>?)value) ?? []).Append("x").ToList();
    }
    if (type == typeof(IList<FileInfo>)) {
      return (((IEnumerable<FileInfo>?)value) ?? []).Append(new FileInfo("x")).ToList();
    }
    throw new NotSupportedException("no toggle for option values of type " + type);
  }

  private static IEnumerable<Command> Commands(Command command) =>
    new[] { command }.Concat(command.Subcommands.SelectMany(Commands));
}
