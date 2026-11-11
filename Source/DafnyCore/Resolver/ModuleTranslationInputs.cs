#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>The builtin portion of a module's resolved dependency closure.</summary>
internal sealed class ModuleTranslationInputs {
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
    void Visit(INode? node) {
      if (node == null || !visited.Add(node)) { return; }
      if (node is ModuleDecl nested && !ReferenceEquals(module, nested)) { return; }
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
        BuiltinNames.Add(declaration.Name);
        if (declaration is ArrowTypeDecl arrow) { AddArrow(arrow.Arity); }
        if (declaration is TupleTypeDecl tuple && tuple.NonGhostTupleTypeDecl != null) {
          BuiltinNames.Add(tuple.NonGhostTupleTypeDecl.Name);
        }
        if (declaration is ArrayClassDecl array) { AddArrow(array.Dims); }
      }
      if (node is Function function) { AddArrow(function.Ins.Count); }
      foreach (var child in node.Children) { Visit(child); }
    }
    Visit(module);
  }

  private void AddArrow(int arity) {
    BuiltinNames.Add(ArrowType.ArrowTypeName(arity));
    BuiltinNames.Add(ArrowType.PartialArrowTypeName(arity));
    BuiltinNames.Add(ArrowType.TotalArrowTypeName(arity));
  }
}
