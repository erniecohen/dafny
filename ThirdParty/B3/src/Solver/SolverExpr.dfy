module SolverExpr {
  import opened Std.Wrappers
  import Std.Collections.Seq
  import opened Basics
  import Types
  import opened DeclarationMarkers

  export
    reveals SDeclaration, SDeclaration.name
    reveals SType, STypeDecl
    provides SType.TypesToSExpr, SType.ToSExpr, SType.ToString
    reveals STypedDeclaration, STypedDeclaration.inputTypes, STypedDeclaration.typ
    reveals SConstant
    provides SConstant.Function
    reveals SExprPrintConfig
    reveals SExpr
    provides SExpr.ToString
    provides SExpr.Boolean, SExpr.Integer, SExpr.Rational, SExpr.Bitvector, SExpr.IndexedFuncAppl, SExpr.EQ, SExpr.Id, SExpr.FuncAppl, SExpr.Eq, SExpr.Negation, SExpr.BigAnd
    provides Types, Wrappers, DeclarationMarkers

  trait SDeclaration extends object {
    const name: string
  }

  class STypeDecl extends SDeclaration, DeclarationMarker {
    constructor (name: string) {
      this.name := name;
    }
  }

  datatype SType =
    | SBool
    | SInt
    | SReal
    | SBitvector(width: Types.BitvectorWidth)
    | SUserType(decl: STypeDecl)
  {
    static function TypesToSExpr(types: seq<SType>): SExpr {
      PP(SeqMap(types, (typ: SType) => typ.ToSExpr()))
    }

    function ToSExpr(): SExpr {
      match this
      case SBool => S("Bool")
      case SInt => S("Int")
      case SReal => S("Real")
      case SBitvector(width) => PP([S("_"), S("BitVec"), SExpr.Integer(width)])
      case SUserType(name) => S(decl.name)
    }

    function ToString(): string {
      match this
      case SBool => "bool"
      case SInt => "int"
      case SReal => "real"
      case SBitvector(width) => Types.BitvectorTypeName(width)
      case SUserType(name) => decl.name
    }
  }

  trait STypedDeclaration extends SDeclaration {
    const typ: SType
    const inputTypes: seq<SType>
  }

  class SConstant extends STypedDeclaration, DeclarationMarker {
    constructor (name: string, typ: SType) {
      this.name := name;
      this.typ := typ;
      this.inputTypes := [];
    }

    constructor Function(name: string, inputTypes: seq<SType>, typ: SType) {
      this.name := name;
      this.typ := typ;
      this.inputTypes := inputTypes;
    }
  }

  datatype SExprPrintConfig = SExprPrintConfig(newlines: bool, indent: string)
  {
    function ExtraIdent(n: nat): SExprPrintConfig {
      if !newlines then this
      else this.(indent := indent + seq(n, i => ' '))
    }
    function Space(): string {
      if newlines then "\n" + indent
      else " "
    }
  }

  datatype SExpr = // pun: solver expression
    | S(string) // Single name
    | PP(seq<SExpr>) // Parentheses
  {
    opaque function ToString(config: SExprPrintConfig := SExprPrintConfig(false, "")): string {
      match this
      case S(name) =>
        if name == "" then "()" else name
      case PP(s) =>
        if |s| == 0 then
          "()"
        else
          var name := s[0].ToString(config);
          var newIndent := config.ExtraIdent(2);
          "(" + name +
          Basics.SeqToString(s[1..], (argument: SExpr) requires argument < this =>
                                newIndent.Space() + argument.ToString(newIndent)
          ) + ")"
    }
    // SExpr builders

    static const FALSE := "false"
    static const TRUE := "true"
    static const EQ := "="
    static const NOT := "not"
    static const AND := "and"

    static function Boolean(b: bool): SExpr {
      S(if b then TRUE else FALSE)
    }
    static function Integer(x: int): SExpr {
      if x < 0 then PP([S("-"), S(Int2String(-x))]) else S(Int2String(x))
    }
    static function Rational(numerator: int, denominator: int): SExpr
      requires denominator > 0
    {
      FuncAppl("/", [FuncAppl("to_real", [Integer(numerator)]), FuncAppl("to_real", [Integer(denominator)])])
    }
    static function Bitvector(word: Types.CanonicalWord): SExpr {
      PP([S("_"), S("bv" + Int2String(word.value)), Integer(word.width)])
    }
    static function IndexedFuncAppl(op: string, indices: seq<int>, args: seq<SExpr>): SExpr
      requires forall i <- indices :: i >= 0
    {
      var head := PP([S("_"), S(op)] + SeqMap(indices, (i: int) => Integer(i)));
      PP([head] + args)
    }
    static function Id(x: SConstant): SExpr {
      S(x.name)
    }
    static function FuncAppl(op: string, args: seq<SExpr>): SExpr {
      if |args| == 0 then
        S(op)
      else
        PP([S(op)] + args)
    }
    static function Eq(e0: SExpr, e1: SExpr): SExpr {
      PP([S(EQ), e0, e1])
    }
    static function Negation(e: SExpr): SExpr {
      PP([S(NOT), e])
    }
    static function BigAnd(ee: seq<SExpr>): SExpr {
      if |ee| == 0 then
        Boolean(true)
      else if |ee| == 1 then
        ee[0]
      else
        PP([S(AND)] + ee)
    }
  }
}