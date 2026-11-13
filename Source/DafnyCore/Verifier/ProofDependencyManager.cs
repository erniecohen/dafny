//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using DafnyCore.Verifier;

namespace Microsoft.Dafny {
  public class ProofDependencyManager {
    // proof dependency tracking state
    public Dictionary<string, ProofDependency> ProofDependenciesById { get; } = new();
    public readonly Dictionary<string, (Declaration Decl, HashSet<ProofDependency> Deps)> idsByMemberName = new();
    private UInt64 proofDependencyIdCount = 0;
    private readonly string idPrefix = "";

    public ProofDependencyManager() { }

    private ProofDependencyManager(ProofDependencyManager shared, string moduleName) {
      ProofDependenciesById = shared.ProofDependenciesById;
      idsByMemberName = shared.idsByMemberName;
      SyncRoot = shared.SyncRoot;
      idPrefix = $"{moduleName.Length}${moduleName}$";
    }

    // A private producer's proof inventory must not renumber client obligations.
    // Keep the reporting tables shared, but scope on-mode translation state to a module.
    public ProofDependencyManager ForModule(string moduleName) => new(this, moduleName);

    /// <summary>
    /// Guards the two maps above.  With --verification-module-window K > 1 a later module is
    /// translated, which adds ids, while an earlier module's results are being reported, which
    /// reads them; readers that run during verification take this lock around their reads.  The
    /// maps stay ordinary dictionaries, whose insertion order the coverage report relies on.
    /// </summary>
    public object SyncRoot { get; } = new();
    private string currentDeclarationString;
    private Declaration currentDeclaration;

    public string GetProofDependencyId(ProofDependency dep) {
      lock (SyncRoot) {
        var idString = $"{idPrefix}id{proofDependencyIdCount}";
        ProofDependenciesById[idString] = dep;
        proofDependencyIdCount++;
        var (_, currentSet) = idsByMemberName.GetOrCreate(currentDeclarationString, () => (currentDeclaration, new HashSet<ProofDependency>()));
        currentSet.Add(dep);
        return idString;
      }
    }

    public void SetCurrentDefinition(string verificationScopeId, Declaration decl) {
      currentDeclarationString = verificationScopeId;
      currentDeclaration = decl;
    }

    public IEnumerable<ProofDependency> GetPotentialDependenciesForDefinition(string defName) {
      return idsByMemberName.TryGetValue(defName, out var result) ? result.Deps : Enumerable.Empty<ProofDependency>();
    }

    public IEnumerable<ProofDependency> GetAllPotentialDependencies() {
      return idsByMemberName.Values.SelectMany(x => x.Deps);
    }

    // The "id" attribute on a Boogie AST node is used by Boogie to label
    // that AST node in the SMT-Lib formula when constructing a verification
    // condition.
    private const string idAttributeName = "id";

    public void AddProofDependencyId(ICarriesAttributes boogieNode, IOrigin tok, ProofDependency dep) {
      var idString = GetProofDependencyId(dep);
      boogieNode.Attributes =
        new QKeyValue(tok, idAttributeName, new List<object>() { idString }, boogieNode.Attributes);
    }

    public ProofDependency GetFullIdDependency(TrackedNodeComponent component) {
      if (component is TrackedCallRequiresGoal requiresGoal) {
        var reqId = ProofDependenciesById[requiresGoal.requiresId];
        var callId = ProofDependenciesById[requiresGoal.callId];
        return new CallRequiresDependency((CallDependency)callId, (RequiresDependency)reqId);
      } else if (component is TrackedCallRequiresAssumed requiresAssumed) {
        var reqId = ProofDependenciesById[requiresAssumed.requiresId];
        var callId = ProofDependenciesById[requiresAssumed.callId];
        return new CallRequiresDependency((CallDependency)callId, (RequiresDependency)reqId);
      } else if (component is TrackedCallEnsures callEnsures) {
        var ensId = ProofDependenciesById[callEnsures.ensuresId];
        var callId = ProofDependenciesById[callEnsures.callId];
        return new CallEnsuresDependency((CallDependency)callId, (EnsuresDependency)ensId);
      } else if (component is TrackedInvariantEstablished invariantEstablished) {
        return ProofDependenciesById[invariantEstablished.invariantId];
      } else if (component is TrackedInvariantMaintained invariantMaintained) {
        return ProofDependenciesById[invariantMaintained.invariantId];
      } else if (component is TrackedInvariantAssumed invariantAssumed) {
        return ProofDependenciesById[invariantAssumed.invariantId];
      } else if (component is LabeledNodeComponent labeledNodeComponent) {
        return ProofDependenciesById[labeledNodeComponent.SolverLabel];
      } else {
        throw new ArgumentException($"Malformed dependency ID: {component.SolverLabel}");
      }
    }

    public IEnumerable<ProofDependency> GetOrderedFullDependencies(IEnumerable<TrackedNodeComponent> components) {
      return components
        .Select(GetFullIdDependency)
        .OrderBy(dep => dep.Range)
        .ThenBy(dep => dep.Description);
    }
  }
}