#nullable enable
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny;

[SyntaxBaseType(typeof(Declaration))]
public class Lemma : Method {
  public override string WhatKind => "lemma";
  public override string WhatKindMentionGhost => WhatKind;

  [SyntaxConstructor]
  public Lemma(IOrigin origin, Name nameNode,
    bool hasStaticKeyword,
    [Captured] List<TypeParameter> typeArgs,
    [Captured] List<Formal> ins, [Captured] List<Formal> outs,
    [Captured] List<AttributedExpression> req,
    [Captured] Specification<FrameExpression> reads,
    [Captured] Specification<FrameExpression> mod,
    [Captured] List<AttributedExpression> ens,
    [Captured] Specification<Expression> decreases,
    [Captured] BlockStmt body,
    Attributes? attributes, IOrigin? signatureEllipsis)
    : base(origin, nameNode, attributes, hasStaticKeyword, true,
      typeArgs, ins, req, ens, reads, decreases, outs, mod, body, signatureEllipsis) {
  }

  public Lemma(Cloner cloner, Lemma lemma) : base(cloner, lemma) {
  }

  public override bool AllowsAllocation => false;

  private bool? postconditionUsesHeap;
  private bool computingPostconditionUsesHeap;
  private static int postconditionUsesHeapComputations;
  private static readonly object PostconditionUsesHeapLock = new();

  /// <summary>
  /// Whether the postcondition of the lemma, as an expression, depends on the heap. If it does, then so do the
  /// results of the lemma when they are bound in an expression (see LemmaResultExpr). A postcondition that binds
  /// the results of another lemma depends on the heap if the postcondition of that lemma does. To be called after
  /// resolution.
  /// </summary>
  public bool PostconditionUsesHeap(DafnyOptions options) {
    // Lemmas are shared by the translations of different modules, which may run at the same time.
    lock (PostconditionUsesHeapLock) {
      if (postconditionUsesHeap is { } known) {
        return known;
      }
      if (computingPostconditionUsesHeap) {
        // The postcondition binds results of a lemma whose own postcondition leads back to this lemma. What this
        // postcondition itself uses is found by the computation that is in progress.
        return false;
      }
      bool usesHeap = false, usesOldHeap = false;
      Type? usesThis = null;
      var freeVariables = new HashSet<IVariable>();
      var freeHeapAtVariables = new HashSet<Label>();
      computingPostconditionUsesHeap = true;
      postconditionUsesHeapComputations++;
      try {
        foreach (var postcondition in Ens) {
          FreeVariablesUtil.ComputeFreeVariables(options, postcondition.E, freeVariables, ref usesHeap, ref usesOldHeap,
            freeHeapAtVariables, ref usesThis, false);
        }
      } finally {
        postconditionUsesHeapComputations--;
        computingPostconditionUsesHeap = false;
      }
      var result = usesHeap || usesOldHeap || freeHeapAtVariables.Count != 0;
      if (result || postconditionUsesHeapComputations == 0) {
        // "true" is right wherever it is found. "false" is right for the lemma that the computation started
        // from; for another lemma, it may have left out a lemma whose computation was in progress.
        postconditionUsesHeap = result;
      }
      return result;
    }
  }
}

public class TwoStateLemma : Method {
  public override string WhatKind => "twostate lemma";
  public override string WhatKindMentionGhost => WhatKind;

  public TwoStateLemma(IOrigin origin, Name nameNode,
    bool hasStaticKeyword,
    [Captured] List<TypeParameter> typeArgs,
    [Captured] List<Formal> ins, [Captured] List<Formal> outs,
    [Captured] List<AttributedExpression> req,
    [Captured] Specification<FrameExpression> reads,
    [Captured] Specification<FrameExpression> mod,
    [Captured] List<AttributedExpression> ens,
    [Captured] Specification<Expression> decreases,
    [Captured] BlockStmt body,
    Attributes attributes, IOrigin signatureEllipsis)
    : base(origin, nameNode, attributes, hasStaticKeyword, true, typeArgs, ins, req, ens, reads, decreases, outs, mod, body, signatureEllipsis) {
    Contract.Requires(origin != null);
    Contract.Requires(nameNode != null);
    Contract.Requires(typeArgs != null);
    Contract.Requires(ins != null);
    Contract.Requires(outs != null);
    Contract.Requires(req != null);
    Contract.Requires(mod != null);
    Contract.Requires(ens != null);
    Contract.Requires(decreases != null);
  }

  public TwoStateLemma(Cloner cloner, TwoStateLemma lemma) : base(cloner, lemma) {
  }

  public override bool AllowsAllocation => false;
}