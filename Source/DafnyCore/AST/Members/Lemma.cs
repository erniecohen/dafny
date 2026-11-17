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

  /// <summary>
  /// Whether the postcondition of the lemma, as an expression, depends on the heap. If it does, then so do the
  /// results of the lemma when they are bound in an expression (see LemmaResultExpr). To be called after resolution.
  /// </summary>
  public bool PostconditionUsesHeap(DafnyOptions options) {
    if (postconditionUsesHeap == null) {
      if (computingPostconditionUsesHeap) {
        // The postcondition binds results whose own postcondition leads back here. The answer "yes" is always safe.
        return true;
      }
      computingPostconditionUsesHeap = true;
      bool usesHeap = false, usesOldHeap = false;
      Type? usesThis = null;
      var freeVariables = new HashSet<IVariable>();
      var freeHeapAtVariables = new HashSet<Label>();
      foreach (var postcondition in Ens) {
        FreeVariablesUtil.ComputeFreeVariables(options, postcondition.E, freeVariables, ref usesHeap, ref usesOldHeap,
          freeHeapAtVariables, ref usesThis, false);
      }
      computingPostconditionUsesHeap = false;
      postconditionUsesHeap = usesHeap || usesOldHeap || freeHeapAtVariables.Count != 0;
    }
    return postconditionUsesHeap.Value;
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