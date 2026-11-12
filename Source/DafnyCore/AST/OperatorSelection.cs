using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Dafny;

// A selection contains declaration identities and immutable type recipes, never inference
// proxies or local-variable objects. M1 excludes refinement and abstract signature transport.
public sealed record OperatorTypeView(TopLevelDecl Head, ImmutableArray<OperatorTypeView> Arguments) {
  public static OperatorTypeView Freeze(PreType type) {
    if (type.NormalizeWrtScope() is not DPreType d) {
      throw new InvalidOperationException("operator type view is not determined");
    }
    return new OperatorTypeView(d.Decl, d.Arguments.Select(Freeze).ToImmutableArray());
  }

  public OperatorTypeView Transport(OperatorCloneContext context) =>
    new(context.Map(Head), Arguments.Select(a => a.Transport(context)).ToImmutableArray());

  public bool Matches(PreType type) => type.NormalizeWrtScope() is DPreType d &&
    ReferenceEquals(Head, d.Decl) && Arguments.Length == d.Arguments.Count &&
    Arguments.Zip(d.Arguments).All(pair => pair.First.Matches(pair.Second));
}

public abstract record OperatorSelection {
  public abstract OperatorSelection Transport(OperatorCloneContext context);
}

public sealed record IntrinsicOperatorSelection(BinaryExpr.ResolvedOpcode? Opcode,
  string UnaryFamily, ImmutableArray<OperatorTypeView> Operands) : OperatorSelection {
  public override OperatorSelection Transport(OperatorCloneContext context) =>
    this with { Operands = Operands.Select(t => t.Transport(context)).ToImmutableArray() };
}

public sealed record MemberOperatorSelection(Function Member, OperatorTypeView Receiver, OperatorDescriptor Descriptor) : OperatorSelection {
  public override OperatorSelection Transport(OperatorCloneContext context) =>
    this with { Member = context.Map(Member), Receiver = Receiver.Transport(context) };
}
