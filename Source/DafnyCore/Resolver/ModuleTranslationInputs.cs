#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Dafny;

/// <summary>The builtin portion of a module's resolved dependency closure.</summary>
internal sealed class ModuleTranslationInputs {
  // Reflection metadata only: this cache never contains source or resolved nodes.
  private static readonly ConcurrentDictionary<System.Type, FieldInfo[]> NodeFields = new();
  public HashSet<string> BuiltinNames { get; } = new(StringComparer.Ordinal) {
    "string", "nat", "object", "array", "_tuple#0", "_tuple#2", "bool", "char", "int", "real", "ORDINAL",
    "_bv", "set", "iset", "seq", "multiset", "map", "imap"
  };
  public SortedSet<int> Bitwidths { get; } = new();
  public HashSet<string> ModuleNames { get; } = new(StringComparer.Ordinal);

  public ModuleTranslationInputs(ModuleDecl? module, IEnumerable<ModuleTranslationInputs> prerequisites) {
    AddArrow(0);
    AddArrow(1);
    foreach (var prerequisite in prerequisites) {
      BuiltinNames.UnionWith(prerequisite.BuiltinNames);
      Bitwidths.UnionWith(prerequisite.Bitwidths);
      ModuleNames.UnionWith(prerequisite.ModuleNames);
    }
    var visited = new HashSet<INode>(ReferenceEqualityComparer.Instance);
    var owner = (module as LiteralModuleDecl)?.ModuleDef;
    void Visit(INode? node) {
      if (node == null || !visited.Add(node)) { return; }
      if (node is ModuleDecl nested && !ReferenceEquals(module, nested)) { return; }
      if (node is ModuleDefinition definition && !ReferenceEquals(owner, definition)) { return; }
      if (node is TopLevelDecl builtin && builtin.EnclosingModuleDefinition?.Name == "_System") {
        AddBuiltin(builtin);
        return;
      }
      if (node is MemberDecl member && !ReferenceEquals(owner, member.EnclosingClass?.EnclosingModuleDefinition)) { return; }
      if (node is TopLevelDecl top && top is not ModuleDecl && top.EnclosingModuleDefinition != null &&
          !ReferenceEquals(owner, top.EnclosingModuleDefinition)) { return; }
      if (node is Expression expression && expression.UnnormalizedType != null) {
        Visit(expression.UnnormalizedType);
      }
      if (node is IVariable variable) { Visit(variable.UnnormalizedType); }
      if (node is TypeProxy { T: { } target }) { Visit(target); }
      if (node is BitvectorType bitvector) {
        Bitwidths.Add(bitvector.Width);
        // Shift lowering can introduce the width's bit-length even when its
        // converted expression is not exposed as a structural AST child.
        Bitwidths.Add((int)new System.Numerics.BigInteger(bitvector.Width).GetBitLength());
      }
      if (node is UserDefinedType { ResolvedClass: { } declaration } && declaration.EnclosingModuleDefinition?.Name == "_System") {
        AddBuiltin(declaration);
      }
      if (node is Function function) { AddArrow(function.Ins.Count); }
      if (node is AllocateArray { ElementInit: not null, ArrayDimensions: { } dimensions }) { AddArrow(dimensions.Count); }
      foreach (var child in node.Children ?? []) { Visit(child); }
      // Children deliberately omits some lowered/resolved structures and target
      // types (notably conversions in newtype constraints and array callbacks).
      // Translation consumes those fields too. Inspect node-valued fields without
      // following enclosing modules or imported declaration graphs.
      foreach (var field in NodeFields.GetOrAdd(node.GetType(), type => {
        var fields = new List<FieldInfo>();
        for (var current = type; current != null; current = current.BaseType) {
          fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(f => typeof(INode).IsAssignableFrom(f.FieldType) ||
              (f.FieldType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(f.FieldType))));
        }
        return fields.ToArray();
      })) {
        switch (field.GetValue(node)) {
          case INode child: Visit(child); break;
          case IEnumerable sequence:
            foreach (var item in sequence) { if (item is INode element) { Visit(element); } }
            break;
        }
      }
    }
    Visit(module);
  }

  private void AddBuiltin(TopLevelDecl declaration) {
    BuiltinNames.Add(declaration.Name);
    if (declaration is ArrowTypeDecl arrow) { AddArrow(arrow.Arity); }
    if (ArrowType.IsPartialArrowTypeName(declaration.Name) || ArrowType.IsTotalArrowTypeName(declaration.Name)) {
      AddArrow(declaration.TypeArgs.Count - 1);
    }
    if (declaration is TupleTypeDecl { NonGhostTupleTypeDecl: { } tuple }) { BuiltinNames.Add(tuple.Name); }
    if (declaration is ArrayClassDecl array) { AddArrow(array.Dims); }
  }

  private void AddArrow(int arity) {
    BuiltinNames.Add(ArrowType.ArrowTypeName(arity));
    BuiltinNames.Add(ArrowType.PartialArrowTypeName(arity));
    BuiltinNames.Add(ArrowType.TotalArrowTypeName(arity));
  }
}
