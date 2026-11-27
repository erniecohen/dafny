#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Boogie;

namespace Microsoft.Dafny.Caching;

internal sealed record StoredModelElement(
  [property: JsonRequired] Model.ElementKind Kind, [property: JsonRequired] string Value,
  [property: JsonRequired] int Id, [property: JsonRequired] bool Registered,
  [property: JsonRequired] List<int> Arguments,
  [property: JsonRequired] List<StoredModelReference> References);
internal sealed record StoredModelReference(
  [property: JsonRequired] int Function, [property: JsonRequired] int Application);
internal sealed record StoredModelApplication(
  [property: JsonRequired] int Result, [property: JsonRequired] List<int> Arguments);
internal sealed record StoredModelFunction(
  [property: JsonRequired] string Name, [property: JsonRequired] int? Arity,
  [property: JsonRequired] List<StoredModelApplication> Applications, [property: JsonRequired] int? Otherwise);
internal sealed record StoredModelVariable(
  [property: JsonRequired] string Name, [property: JsonRequired] int? Element);
internal sealed record StoredModelState(
  [property: JsonRequired] string Name, [property: JsonRequired] List<StoredModelVariable> Variables,
  [property: JsonRequired] StoredDiagnosticRange? Location, [property: JsonRequired] string? Suffix);
internal sealed record StoredModel(
  [property: JsonRequired] List<StoredModelElement> Elements, [property: JsonRequired] List<StoredModelFunction> Functions,
  [property: JsonRequired] List<StoredModelState> States, [property: JsonRequired] bool HasStates);
internal sealed record StoredFailureModel(
  [property: JsonRequired] StoredModel Model, [property: JsonRequired] bool Render);

/// <summary>
/// Ordered native model data. Unlike printed model text, this preserves element numbering,
/// function/application order, aliases and state-variable order. No solver or AST objects persist.
/// </summary>
internal static class PersistentModels {
  // The pinned native parser constructs unregistered datatype expressions this way.
  // Use only this explicit constructor; a changed native ABI makes the record a miss.
  private static readonly ConstructorInfo? DatatypeConstructor = typeof(Model.DatatypeValue).GetConstructor(
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
    [typeof(Model), typeof(string), typeof(List<Model.Element>)], null);

  private static StoredModel Data(Model model, List<StoredModelState> states) {
    var references = model.Functions.SelectMany((f, i) => f.Apps.Select((a, j) => (a, Reference: new StoredModelReference(i, j))))
      .ToDictionary(p => p.a, p => p.Reference);
    var registered = new HashSet<Model.Element>(model.Elements, ReferenceEqualityComparer.Instance);
    var all = model.Elements.ToList();
    var indices = new Dictionary<Model.Element, int>(ReferenceEqualityComparer.Instance);
    for (var i = 0; i < all.Count; i++) { indices.Add(all[i], i); }
    int? Index(Model.Element? element) {
      if (element == null) { return null; }
      if (!ReferenceEquals(element.Model, model)) { throw new ArgumentException("foreign model element"); }
      if (!indices.TryGetValue(element, out var index)) {
        indices.Add(element, index = all.Count);
        all.Add(element);
      }
      return index;
    }
    var functions = model.Functions.Select(f => new StoredModelFunction(f.Name, f.Arity,
      f.Apps.Select(a => new StoredModelApplication(Index(a.Result)!.Value, a.Args.Select(e => Index(e)!.Value).ToList())).ToList(), Index(f.Else))).ToList();
    var captured = model.States.Select((state, i) => states[i] with {
      Name = state.Name,
      Variables = state.Variables.Select(v => new StoredModelVariable(v, Index(state.TryGet(v)))).ToList()
    }).ToList();
    for (var i = 0; i < all.Count; i++) {
      if (all[i] is Model.DatatypeValue value) { foreach (var argument in value.Arguments) { Index(argument); } }
    }
    return new StoredModel(
      all.Select(e => new StoredModelElement(e.Kind,
        e is Model.DatatypeValue value ? value.ConstructorName : e.ToString(),
        e.Id, registered.Contains(e),
        e is Model.DatatypeValue datatype ? datatype.Arguments.Select(a => Index(a)!.Value).ToList() : [],
        e.References.Select(r => references[r]).ToList())).ToList(),
      functions, captured, model.ModelHasStatesAlready);
  }

  internal static StoredModel? Capture(SourceLocations locations, Model model, IReadOnlyDictionary<string, (TokenRange Range, string Suffix)> bindings) {
    try {
      var states = new List<StoredModelState>();
      foreach (var state in model.States) {
        StoredDiagnosticRange? location = null;
        string? suffix = null;
        if (state != model.InitialState) {
          if (!bindings.TryGetValue(state.Name, out var binding) ||
              PersistentFailureReports.CaptureRange(locations, binding.Range) is not { } range ||
              !locations.PortableReportText(binding.Suffix)) { return null; }
          location = range;
          suffix = binding.Suffix;
        }
        states.Add(new StoredModelState(state.Name,
          [], location, suffix));
      }
      var stored = Data(model, states);
      if (Restore(locations, stored, out _) == null) { return null; }
      return stored;
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  internal static Model? Restore(SourceLocations locations, StoredModel stored, out IReadOnlyDictionary<string, TokenRange> ranges) {
    ranges = new Dictionary<string, TokenRange>();
    try {
      if (stored?.Elements is not { Count: >= 2 } || stored.Functions == null || stored.States is not { Count: > 0 }) { return null; }
      var model = new Model { ModelHasStatesAlready = stored.HasStates };
      foreach (var function in stored.Functions) {
        if (function == null || string.IsNullOrEmpty(function.Name) || function.Arity < 0 || function.Applications == null ||
            model.HasFunc(function.Name)) { return null; }
        model.MkFunc(function.Name, function.Arity);
      }
      if (stored.Elements.Any(e => e == null || string.IsNullOrEmpty(e.Value) || !Enum.IsDefined(e.Kind) ||
          e.Arguments == null || e.Kind != Model.ElementKind.DataValue && e.Arguments.Count != 0)) { return null; }
      var registered = stored.Elements.TakeWhile(e => e.Registered).ToArray();
      if (registered.Length < 2 || stored.Elements.Skip(registered.Length).Any(e => e.Registered)) { return null; }
      var elements = new Model.Element[stored.Elements.Count];
      Model.Element Element(int index) => index >= 0 && index < elements.Length && elements[index] != null
        ? elements[index] : throw new ArgumentException("model element index");
      var orphans = stored.Elements.Select((e, i) => (Entry: e, Index: i)).Where(p => !p.Entry.Registered).ToLookup(p => p.Entry.Id);
      if (orphans.Any(g => g.Key < 2 || g.Key > registered.Length)) { return null; }
      for (var i = 0; i <= registered.Length; i++) {
        var pending = new HashSet<int>();
        Model.Element Construct(int index) {
          if (index < 0 || index >= elements.Length) { throw new ArgumentException("model argument index"); }
          if (elements[index] != null) { return elements[index]; }
          var entry = stored.Elements[index];
          if (entry.Registered || entry.Id != i || !pending.Add(index)) { throw new ArgumentException("model construction order"); }
          var value = entry.Kind == Model.ElementKind.DataValue && entry.Arguments.Count != 0
            ? (Model.Element?)DatatypeConstructor?.Invoke([model, entry.Value, entry.Arguments.Select(Construct).ToList()])
            : model.ConstructElement(entry.Value);
          pending.Remove(index);
          return elements[index] = value ?? throw new ArgumentException("native datatype constructor");
        }
        // ConstructElement does not intern/register its result. Native state bindings use it
        // for literals, whose IDs may collide with a subsequently registered element's ID.
        foreach (var orphan in orphans[i]) {
          Construct(orphan.Index);
        }
        if (i < registered.Length) {
          if (registered[i].Arguments.Count != 0) { return null; }
          elements[i] = model.MkElement(registered[i].Value);
        }
      }
      for (var i = 0; i < elements.Length; i++) {
        if (elements[i] == null || elements[i].Id != stored.Elements[i].Id || elements[i].Kind != stored.Elements[i].Kind) { return null; }
      }
      foreach (var function in stored.Functions) {
        var current = model.GetFunc(function.Name);
        foreach (var application in function.Applications) {
          if (application?.Arguments == null || function.Arity != application.Arguments.Count) { return null; }
          current.AddApp(Element(application.Result), application.Arguments.Select(Element).ToArray());
        }
        if (function.Otherwise is { } otherwise) { current.Else = Element(otherwise); }
      }
      var applications = model.Functions.Select(f => f.Apps.ToArray()).ToArray();
      for (var i = 0; i < elements.Length; i++) {
        // Native substitutions can leave reverse links in their original order, even when
        // they no longer name the substituted element. Preserve those observable links too.
        if (stored.Elements[i].References == null || elements[i].References is not ICollection<Model.FuncTuple> references || references.IsReadOnly) { return null; }
        references.Clear();
        foreach (var reference in stored.Elements[i].References) {
          if (reference == null || reference.Function < 0 || reference.Function >= applications.Length ||
              reference.Application < 0 || reference.Application >= applications[reference.Function].Length) { return null; }
          references.Add(applications[reference.Function][reference.Application]);
        }
      }
      var bound = new Dictionary<string, TokenRange>(StringComparer.Ordinal);
      for (var i = 0; i < stored.States.Count; i++) {
        var state = stored.States[i];
        if (state == null || string.IsNullOrEmpty(state.Name) || state.Variables == null) { return null; }
        if (i == 0) {
          if (state.Name != model.InitialState.Name || state.Location != null || state.Suffix != null) { return null; }
        } else {
          if (state.Location == null || state.Suffix == null || !locations.PortableReportText(state.Suffix) ||
              PersistentFailureReports.RestoreRange(locations, state.Location) is not { } range ||
              state.Name == model.InitialState.Name || !bound.TryAdd(state.Name, range)) { return null; }
        }
        var current = i == 0 ? model.InitialState : model.MkState(state.Name);
        foreach (var variable in state.Variables) {
          if (variable == null || string.IsNullOrEmpty(variable.Name)) { return null; }
          current.AddBinding(variable.Name, variable.Element is { } index ? Element(index) : null!);
        }
      }
      // Detect implicit extra elements/functions, normalized literals and incomplete reconstruction.
      if (JsonSerializer.Serialize(Data(model, stored.States)) != JsonSerializer.Serialize(stored)) { return null; }
      ranges = bound;
      return model;
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }

  internal static DafnyModel? Restore(SourceLocations locations, StoredModel stored, DafnyOptions options) {
    try {
      if (Restore(locations, stored, out var ranges) is not { } model) { return null; }
      var descriptions = stored.States.Skip(1).ToDictionary(s => s.Name,
        s => ranges[s.Name].ToFileRangeString(options) + s.Suffix, StringComparer.Ordinal);
      return new DafnyModel(model, options, descriptions);
    } catch (Exception error) when (error is not OutOfMemoryException) { return null; }
  }
}
