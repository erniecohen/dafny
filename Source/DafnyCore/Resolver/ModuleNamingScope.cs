#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Dafny;

/// <summary>
/// Synchronous worker/translator state. Imported declarations never lend their mutable
/// name generators or cached variable/label names to another module under the opt-in path.
/// </summary>
internal sealed class ModuleNamingScope : IDisposable {
  [ThreadStatic] private static ModuleNamingScope? current;
  public static ModuleNamingScope? Current => current;
  private readonly ModuleNamingScope? previous;
  private readonly Dictionary<FreshIdGenerator, FreshIdGenerator> generators = new();
  private readonly Dictionary<object, string> names = new();
  private readonly Dictionary<object, string> compileNames = new();
  private readonly Dictionary<(object, string), object> values = new();
  private readonly Dictionary<string, int> counters = new(StringComparer.Ordinal);
  public string Prefix { get; }

  public ModuleNamingScope(string moduleName) {
    previous = current;
    Prefix = "M" + Convert.ToHexStringLower(Encoding.UTF8.GetBytes(moduleName)) + "_";
    current = this;
  }

  public FreshIdGenerator Generator(FreshIdGenerator original, Func<FreshIdGenerator> create) {
    if (!generators.TryGetValue(original, out var generator)) {
      generator = create();
      generators.Add(original, generator);
    }
    return generator;
  }

  public string? Name(object owner) => names.GetValueOrDefault(owner);

  public string Name(object owner, Func<string> create) {
    if (!names.TryGetValue(owner, out var name)) {
      name = create();
      names.Add(owner, name);
    }
    return name;
  }

  public int Next(string kind) {
    var next = counters.GetValueOrDefault(kind);
    counters[kind] = checked(next + 1);
    return next;
  }

  public string CompileName(object owner, Func<string> create) {
    if (!compileNames.TryGetValue(owner, out var name)) {
      name = create();
      compileNames.Add(owner, name);
    }
    return name;
  }

  public T Value<T>(object owner, string kind, Func<T> create) where T : notnull {
    if (!values.TryGetValue((owner, kind), out var value)) {
      value = create();
      values.Add((owner, kind), value);
    }
    return (T)value;
  }

  public T? FindValue<T>(object owner, string kind) where T : class =>
    values.TryGetValue((owner, kind), out var value) ? (T)value : null;

  public void SetValue<T>(object owner, string kind, T value) where T : notnull {
    values[(owner, kind)] = value;
  }

  public void Dispose() {
    current = previous;
  }
}
