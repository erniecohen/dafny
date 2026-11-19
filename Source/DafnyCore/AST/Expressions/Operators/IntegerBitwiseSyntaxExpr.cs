// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// Optional source notation. After type inference its semantic child is an ordinary
/// checked conversion of a compiler-owned reference call, or the original BV/bool
/// operation. Serialization retains the source operator and rebuilds that choice.
/// </summary>
public class IntegerBitwiseSyntaxExpr : ConcreteSyntaxExpression, ICloneable<IntegerBitwiseSyntaxExpr>, ICanFormat {
  public Expression Syntax { get; }

  public IntegerBitwiseSyntaxExpr(IOrigin origin, Expression syntax) : base(origin) {
    Syntax = syntax;
  }

  public IntegerBitwiseSyntaxExpr(Cloner cloner, IntegerBitwiseSyntaxExpr original) : base(cloner, original) {
    Syntax = cloner.CloneExpr(original.Syntax);
  }

  public static bool IsBitwise(BinaryExpr.Opcode op) => op is BinaryExpr.Opcode.BitwiseAnd or
    BinaryExpr.Opcode.BitwiseOr or BinaryExpr.Opcode.BitwiseXor or BinaryExpr.Opcode.LeftShift or BinaryExpr.Opcode.RightShift;

  public override IEnumerable<Expression> PreResolveSubExpressions => [Syntax];
  public IntegerBitwiseSyntaxExpr Clone(Cloner cloner) => new(cloner, this);
  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) =>
    Syntax is not ICanFormat formattable || formattable.SetIndent(indentBefore, formatter);
}
