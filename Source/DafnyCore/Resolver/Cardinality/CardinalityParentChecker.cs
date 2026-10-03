// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Dafny;

internal sealed class CardinalityParentChecker(CardinalityTypeVisitor visitor, ErrorReporter reporter,
  CancellationToken cancellationToken) {
  internal bool Check(CardinalityDeclarationInfo child, CardinalityParentInstance parent) {
    cancellationToken.ThrowIfCancellationRequested();
    var success = true;
    if (parent.Parent is not TraitDecl || child.Modes.Length != child.Declaration.TypeArgs.Count) {
      throw new CardinalityTypeException(parent.Reason.Origin, "inconsistent resolved parent-family descriptor");
    }
    var parentModes = visitor.AdvertisedModes(parent.Parent);
    var childModes = visitor.AdvertisedModes(child.Declaration);
    if (parent.Actuals.Length != parentModes.Length) {
      throw new CardinalityTypeException(parent.Reason.Origin, "inconsistent parent type-argument arity");
    }
    var profiles = new CardinalityProfile[parent.Actuals.Length];
    for (var slot = 0; slot < parent.Actuals.Length; slot++) {
      cancellationToken.ThrowIfCancellationRequested();
      profiles[slot] = visitor.Profile(parent.Actuals[slot], parent.Reason.Through($"parent argument {slot}"));
      foreach (var entry in profiles[slot].Entries) {
        if (entry.Key.IsFormal && (!ReferenceEquals(entry.Key.Owner, child.Declaration) ||
                                  entry.Key.Index >= childModes.Length)) {
          throw new CardinalityTypeException(entry.Value.Reason.Origin,
            "a parent-family descriptor contains an unsubstituted enclosing type parameter");
        }
      }
    }
    if (parent.Parent is TraitDecl { IsReferenceTypeDecl: false }) {
      var retainers = parent.Actuals.Select(visitor.DirectRetainedFormal).ToArray();
      for (var index = 0; index < childModes.Length; index++) {
        cancellationToken.ThrowIfCancellationRequested();
        var atom = CardinalityAtom.Formal(child.Declaration, index);
        var slots = Enumerable.Range(0, retainers.Length)
          .Where(slot => retainers[slot] is { } retained && retained.Equals(atom)).ToArray();
        var parameter = child.Declaration.TypeArgs[index];
        if (slots.Length == 0) {
          Report(ResolutionErrors.ErrorId.r_cardinality_unretained_parameter, parameter,
            $"Type parameter '{parameter.Name}' of '{child.Declaration.FullName}' is not retained as a direct " +
            $"type argument of parent '{parent.Parent.FullName}'. One fixed parent instance must not contain " +
            "implementations for arbitrary type arguments.");
        } else if (!slots.Any(slot => (byte)childModes[index] <= (byte)parentModes[slot])) {
          Report(ResolutionErrors.ErrorId.r_cardinality_parent_contract, parameter,
            $"Type parameter '{parameter.Name}' of '{child.Declaration.FullName}' has a permissive cardinality " +
            $"contract, but every directly retaining argument of parent '{parent.Parent.FullName}' is strict.");
        }
      }
    } else {
      // Reference inheritance does not impose parameter retention or introduce containment edges.
      var exposed = new CardinalityProfile();
      for (var slot = 0; slot < parent.Actuals.Length; slot++) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in profiles[slot].Entries) {
          if (entry.Key.IsFormal) {
            exposed.Add(entry.Key, CardinalityWeights.Join(parentModes[slot], entry.Value.Weight), entry.Value.Reason);
          }
        }
      }
      for (var index = 0; index < childModes.Length; index++) {
        cancellationToken.ThrowIfCancellationRequested();
        if (exposed.TryGet(CardinalityAtom.Formal(child.Declaration, index), out var permitted) &&
            (byte)childModes[index] > (byte)permitted.Weight) {
          var parameter = child.Declaration.TypeArgs[index];
          Report(ResolutionErrors.ErrorId.r_cardinality_parent_contract, parameter,
            $"Type parameter '{parameter.Name}' of '{child.Declaration.FullName}' has a permissive cardinality " +
            $"contract that exceeds its exposed strict contract in reference parent '{parent.Parent.FullName}'.");
        }
      }
    }
    return success;

    void Report(ResolutionErrors.ErrorId errorId, TypeParameter parameter, string message) {
      success = false;
      reporter.MessageCore(new DafnyDiagnostic(MessageSource.Resolver, errorId.ToString(),
        parent.Reason.Origin.ReportingRange, [message], ErrorLevel.Error,
        [new DafnyRelatedInformation(parameter.Origin.ReportingRange, "", ["The child type parameter is declared here."]),
          new DafnyRelatedInformation(parent.Parent.Origin.ReportingRange, "", ["The parent cardinality contract is declared here."])]));
    }
  }
}
