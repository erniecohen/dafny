// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Microsoft.Dafny;

/// <summary>Syntax-directed profiles with explicit stacks and no unfolding of nominal representations.</summary>
internal sealed class CardinalityTypeVisitor {
  private readonly CardinalityCanonicalizer canonicalizer;
  private readonly CancellationToken cancellationToken;
  private readonly Dictionary<TopLevelDecl, ImmutableArray<CardinalityWeight>> builtins = new();
  private readonly Dictionary<TopLevelDecl, int?> identityProjections = new();

  internal CardinalityTypeVisitor(SystemModuleManager system, CardinalityCanonicalizer canonicalizer,
    CancellationToken cancellationToken) {
    this.canonicalizer = canonicalizer;
    this.cancellationToken = cancellationToken;
    foreach (var declaration in system.valuetypeDecls.Concat(system.PreTypeBuiltins.Values.OfType<ValuetypeDecl>())
               .Concat(system.SystemModule.TopLevelDecls.OfType<ValuetypeDecl>())) {
      builtins[declaration] = declaration.TypeArgs.Select(CardinalityWeights.Mode).ToImmutableArray();
    }
    foreach (var declaration in system.arrayTypeDecls.Values) {
      builtins[declaration] = [CardinalityWeight.Preserving];
    }
    foreach (var declaration in system.SystemModule.TopLevelDecls.OfType<TupleTypeDecl>()) {
      builtins[declaration] = Enumerable.Repeat(CardinalityWeight.Preserving, declaration.TypeArgs.Count).ToImmutableArray();
    }
    foreach (var entry in system.ArrowTypeDecls) {
      var modes = Enumerable.Repeat(CardinalityWeight.Expanding, entry.Key)
        .Append(CardinalityWeight.Preserving).ToImmutableArray();
      builtins[entry.Value] = modes;
      if (system.PartialArrowTypeDecls.TryGetValue(entry.Key, out var partial)) {
        builtins[partial] = modes;
      }
      if (system.TotalArrowTypeDecls.TryGetValue(entry.Key, out var total)) {
        builtins[total] = modes;
      }
    }
  }

  internal bool IsBuiltin(TopLevelDecl declaration) => builtins.ContainsKey(declaration);

  internal ImmutableArray<CardinalityWeight> AdvertisedModes(TopLevelDecl declaration) =>
    canonicalizer.AdvertisedModes(declaration);

  internal CardinalityTypeUse Normalize(CardinalityTypeUse use) => Normalize(use, null, null);

  private CardinalityTypeUse Normalize(CardinalityTypeUse use,
    HashSet<(Type, CardinalitySubstitution?)>? ancestors,
    List<(Type, CardinalitySubstitution?)>? entered) {
    var seen = new HashSet<(Type, CardinalitySubstitution?)>();
    while (true) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!seen.Add((use.Type, use.Substitution))) {
        throw new CardinalityTypeException(use.Type.Origin ?? Token.NoToken, "cyclic resolved type or substitution");
      }
      // Record raw and intermediate cursors as well as the normalized result. An internal
      // view can otherwise hide a cyclic actual behind newly allocated substitution environments.
      if (ancestors != null) {
        var cursor = (use.Type, use.Substitution);
        if (!ancestors.Add(cursor)) {
          throw new CardinalityTypeException(use.Type.Origin ?? Token.NoToken, "cyclic resolved type arguments or substitutions");
        }
        entered?.Add(cursor);
      }
      if ((use.Type is BasicType || use.Type is UserDefinedType { ResolvedClass: TypeParameter }) &&
          use.Type.TypeArgs.Count != 0) {
        throw new CardinalityTypeException(use.Type.Origin ?? Token.NoToken,
          "a resolved atomic type has unexpected type arguments");
      }
      switch (use.Type) {
        case TypeProxy { T: { } target }:
          use = new CardinalityTypeUse(target, use.Substitution);
          continue;
        case TypeProxy proxy:
          throw new CardinalityTypeException(proxy.Origin ?? Token.NoToken,
            "an unresolved type proxy reached cardinality validation");
        case UserDefinedType { ResolvedClass: TypeParameter parameter }:
          if (use.Substitution != null && use.Substitution.TryGet(parameter, out var actual)) {
            use = actual;
            continue;
          }
          return use;
        case UserDefinedType { ResolvedClass: InternalTypeSynonymDecl synonym } type:
          // Validate the view link, but use its real RHS and actual substitution, ignoring visibility scopes.
          canonicalizer.Declaration(synonym);
          var actuals = type.TypeArgs.Select(argument => new CardinalityTypeUse(argument, use.Substitution)).ToArray();
          use = new CardinalityTypeUse(synonym.Rhs, CardinalitySubstitution.Bind(synonym, actuals, use.Substitution));
          continue;
        case SelfType { ResolvedType: { } resolved }:
          use = new CardinalityTypeUse(resolved, use.Substitution);
          continue;
        default:
          return use;
      }
    }
  }

  internal CardinalityParentInstance Parent(CardinalityTypeUse use, CardinalityReason reason) {
    use = Normalize(use);
    if (use.Type is not UserDefinedType { ResolvedClass: { } raw } type || raw is TypeParameter) {
      throw new CardinalityTypeException(reason.Origin, "a parent obligation is not a resolved nominal trait instance");
    }
    var parent = canonicalizer.Declaration(raw);
    if (parent is not TraitDecl) {
      throw new CardinalityTypeException(reason.Origin, "a parent obligation does not denote a trait");
    }
    CheckArity(type, parent);
    return new CardinalityParentInstance(parent,
      type.TypeArgs.Select(argument => new CardinalityTypeUse(argument, use.Substitution)).ToImmutableArray(), reason);
  }

  internal CardinalityProfile Profile(CardinalityTypeUse use, CardinalityReason reason) {
    var profile = new CardinalityProfile();
    var pending = new Stack<(CardinalityTypeUse Use, CardinalityWeight Weight, CardinalityReason Reason,
      List<(Type, CardinalitySubstitution?)>? Entered)>();
    var active = new HashSet<(Type, CardinalitySubstitution?)>();
    pending.Push((use, CardinalityWeight.Preserving, reason, null));
    while (pending.TryPop(out var item)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (item.Entered is { } completed) {
        Leave(completed, active);
        continue;
      }
      var entered = new List<(Type, CardinalitySubstitution?)>();
      var current = Normalize(item.Use, active, entered);
      pending.Push((current, item.Weight, item.Reason, entered));
      switch (current.Type) {
        case BasicType:
          break;
        case MapType map:
          if (map.TypeArgs.Count != 2) { throw Unknown(current.Type, item.Reason); }
          Push(map.TypeArgs[1], CardinalityWeight.Preserving, "map range");
          Push(map.TypeArgs[0], map.Finite ? CardinalityWeight.Preserving : CardinalityWeight.Expanding,
            map.Finite ? "map domain" : "imap domain");
          break;
        case SetType set:
          if (set.TypeArgs.Count != 1) { throw Unknown(current.Type, item.Reason); }
          Push(set.TypeArgs[0], set.Finite ? CardinalityWeight.Preserving : CardinalityWeight.Expanding,
            set.Finite ? "set element" : "iset element");
          break;
        case SeqType sequence:
          if (sequence.TypeArgs.Count != 1) { throw Unknown(current.Type, item.Reason); }
          Push(sequence.TypeArgs[0], CardinalityWeight.Preserving, "sequence element");
          break;
        case MultiSetType multiset:
          if (multiset.TypeArgs.Count != 1) { throw Unknown(current.Type, item.Reason); }
          Push(multiset.TypeArgs[0], CardinalityWeight.Preserving, "multiset element");
          break;
        case UserDefinedType { ResolvedClass: TypeParameter parameter }:
          profile.Add(canonicalizer.Formal(parameter), item.Weight, item.Reason);
          break;
        case UserDefinedType { ResolvedClass: { } raw } type:
          var declaration = canonicalizer.Declaration(raw);
          CheckArity(type, declaration);
          if (!builtins.TryGetValue(declaration, out var modes)) {
            if (!CardinalityValidator.IsTypeDeclaration(declaration)) {
              throw Unknown(type, item.Reason);
            }
            profile.Add(CardinalityAtom.Head(declaration), item.Weight, item.Reason);
            modes = AdvertisedModes(declaration);
          }
          for (var index = type.TypeArgs.Count - 1; index >= 0; index--) {
            var step = declaration is ArrowTypeDecl ||
              builtins.ContainsKey(declaration) && declaration is SubsetTypeDecl
              ? index == type.TypeArgs.Count - 1 ? "arrow result" : $"arrow input {index}"
              : declaration is TupleTypeDecl ? $"tuple component {index}"
              : $"type argument {index} of '{declaration.FullName}'";
            Push(type.TypeArgs[index], modes[index], step);
          }
          break;
        default:
          throw Unknown(current.Type, item.Reason);
      }
      continue;

      void Push(Type? argument, CardinalityWeight mode, string step) {
        if (argument == null) {
          throw new CardinalityTypeException(item.Reason.Origin, "a resolved type has a missing type argument");
        }
        pending.Push((new CardinalityTypeUse(argument, current.Substitution),
          CardinalityWeights.Join(item.Weight, mode), item.Reason.Through(step), null));
      }
    }
    return profile;
  }

  private static CardinalityTypeException Unknown(Type type, CardinalityReason reason) {
    var identity = type is UserDefinedType { ResolvedClass: { } declaration }
      ? $" for nominal declaration '{declaration.FullName}' ({declaration.GetType().Name})"
      : type is UserDefinedType nominal ? $" for unresolved name '{nominal.Name}'" : "";
    return new CardinalityTypeException(type.Origin ?? reason.Origin,
      $"unclassified resolved type form '{type.GetType().Name}'{identity}");
  }

  private static void CheckArity(UserDefinedType type, TopLevelDecl declaration) {
    if (type.TypeArgs.Count != declaration.TypeArgs.Count) {
      throw new CardinalityTypeException(type.Origin ?? declaration.Origin,
        "a resolved nominal type has inconsistent type-argument arity");
    }
  }

  internal CardinalityAtom? DirectRetainedFormal(CardinalityTypeUse use) {
    var seen = new HashSet<(Type, CardinalitySubstitution?)>();
    while (true) {
      cancellationToken.ThrowIfCancellationRequested();
      use = Normalize(use, seen, null);
      if (use.Type is UserDefinedType { ResolvedClass: TypeParameter parameter }) {
        return canonicalizer.Formal(parameter);
      }
      if (use.Type is not UserDefinedType { ResolvedClass: { } raw } type) { return null; }
      var declaration = canonicalizer.Declaration(raw);
      // A subset or newtype is not an identity alias.
      if (declaration is not TypeSynonymDecl || declaration is SubsetTypeDecl) { return null; }
      CheckArity(type, declaration);
      var projection = IdentityProjection(declaration);
      if (projection == null) { return null; }
      use = new CardinalityTypeUse(type.TypeArgs[projection.Value], use.Substitution);
    }
  }

  /// <summary>Compute identity aliases once without recursive calls, including aliases that select an argument.</summary>
  private int? IdentityProjection(TopLevelDecl initial) {
    if (identityProjections.TryGetValue(initial, out var cached)) { return cached; }
    var frames = new Stack<IdentityFrame>();
    var active = new HashSet<TopLevelDecl>();
    Enter(initial);
    while (frames.TryPeek(out var frame)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!frame.Normalized) {
        frame.Current = Normalize(frame.Current, frame.Ancestors, null);
        frame.Normalized = true;
      }
      if (frame.Current.Type is UserDefinedType { ResolvedClass: TypeParameter parameter }) {
        var atom = canonicalizer.Formal(parameter);
        Complete(ReferenceEquals(atom.Owner, frame.Declaration) ? atom.Index : null);
      } else if (frame.Current.Type is UserDefinedType { ResolvedClass: { } raw } type &&
                 canonicalizer.Declaration(raw) is TypeSynonymDecl alias && alias is not SubsetTypeDecl) {
        CheckArity(type, alias);
        if (!identityProjections.TryGetValue(alias, out var projection)) {
          if (active.Contains(alias)) {
            throw new CardinalityTypeException(alias.Origin, "cyclic identity type synonyms");
          }
          Enter(alias);
        } else if (projection == null) {
          Complete(null);
        } else {
          if (!frame.Followed.Add((frame.Current.Type, frame.Current.Substitution))) {
            throw new CardinalityTypeException(alias.Origin, "cyclic identity-alias type arguments");
          }
          frame.Current = new CardinalityTypeUse(type.TypeArgs[projection.Value], frame.Current.Substitution);
          frame.Normalized = false;
        }
      } else {
        Complete(null);
      }
    }
    return identityProjections[initial];

    void Enter(TopLevelDecl declaration) {
      active.Add(declaration);
      frames.Push(new IdentityFrame(declaration, new CardinalityTypeUse(((TypeSynonymDecl)declaration).Rhs)));
    }
    void Complete(int? projection) {
      var finished = frames.Pop();
      active.Remove(finished.Declaration);
      identityProjections[finished.Declaration] = projection;
    }
  }

  private sealed class IdentityFrame(TopLevelDecl declaration, CardinalityTypeUse current) {
    internal TopLevelDecl Declaration { get; } = declaration;
    internal CardinalityTypeUse Current = current;
    internal bool Normalized;
    internal HashSet<(Type, CardinalitySubstitution?)> Ancestors { get; } = new();
    internal HashSet<(Type, CardinalitySubstitution?)> Followed { get; } = new();
  }

  /// <summary>Structural parent-instance equality, without visibility-dependent Type.Equals or recursive Subst.</summary>
  internal bool SameType(CardinalityTypeUse left, CardinalityTypeUse right) {
    var pending = new Stack<(CardinalityTypeUse Left, CardinalityTypeUse Right,
      List<(Type, CardinalitySubstitution?)>? EnteredLeft, List<(Type, CardinalitySubstitution?)>? EnteredRight)>();
    var seen = new HashSet<(Type, CardinalitySubstitution?, Type, CardinalitySubstitution?)>();
    var activeLeft = new HashSet<(Type, CardinalitySubstitution?)>();
    var activeRight = new HashSet<(Type, CardinalitySubstitution?)>();
    pending.Push((left, right, null, null));
    while (pending.TryPop(out var pair)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (pair.EnteredLeft is { } completedLeft) {
        Leave(completedLeft, activeLeft);
        Leave(pair.EnteredRight!, activeRight);
        continue;
      }
      var enteredLeft = new List<(Type, CardinalitySubstitution?)>();
      var enteredRight = new List<(Type, CardinalitySubstitution?)>();
      var a = Normalize(pair.Left, activeLeft, enteredLeft);
      var b = Normalize(pair.Right, activeRight, enteredRight);
      pending.Push((a, b, enteredLeft, enteredRight));
      if (!seen.Add((a.Type, a.Substitution, b.Type, b.Substitution))) { continue; }
      if (a.Type is UserDefinedType ua && b.Type is UserDefinedType ub) {
        if (ua.ResolvedClass is TypeParameter pa && ub.ResolvedClass is TypeParameter pb) {
          if (!canonicalizer.Formal(pa).Equals(canonicalizer.Formal(pb))) { return false; }
        } else if (ua.ResolvedClass == null || ub.ResolvedClass == null ||
                   !ReferenceEquals(canonicalizer.Declaration(ua.ResolvedClass), canonicalizer.Declaration(ub.ResolvedClass))) {
          return false;
        }
      } else if (a.Type.GetType() != b.Type.GetType()) {
        return false;
      } else if (a.Type is BasicType && !a.Type.Equals(b.Type)) {
        return false;
      } else if (a.Type is SetType sa && b.Type is SetType sb && sa.Finite != sb.Finite ||
                 a.Type is MapType ma && b.Type is MapType mb && ma.Finite != mb.Finite) {
        return false;
      }
      if (a.Type.TypeArgs.Count != b.Type.TypeArgs.Count) { return false; }
      for (var index = 0; index < a.Type.TypeArgs.Count; index++) {
        pending.Push((new CardinalityTypeUse(a.Type.TypeArgs[index], a.Substitution),
          new CardinalityTypeUse(b.Type.TypeArgs[index], b.Substitution), null, null));
      }
    }
    return true;
  }
  private void Leave(IEnumerable<(Type, CardinalitySubstitution?)> entered,
    HashSet<(Type, CardinalitySubstitution?)> active) {
    foreach (var cursor in entered) {
      cancellationToken.ThrowIfCancellationRequested();
      active.Remove(cursor);
    }
  }

}
