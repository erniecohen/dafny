module Incarnations {
  import opened Std.Wrappers
  import opened Basics
  import opened Ast
  import opened SolverExpr
  import Types
  import RSolvers

  export
    reveals DeclMappings
    provides DeclMappings.Type2SType, DeclMappings.Type2STypeWithMap
    provides Incarnations, Incarnations.Print
    provides Incarnations.Empty, Incarnations.DeclMap
    provides Incarnations.Update, Incarnations.Reserve, Incarnations.Variables, Incarnations.DomainRestrict
    provides Incarnations.Set, Incarnations.CreateSubMap
    provides Incarnations.Get, Incarnations.Substitute, Incarnations.REval, Incarnations.SubstituteVariable, Incarnations.Type2SType
    provides Types, Ast, SolverExpr, RSolvers

  type RExpr = RSolvers.RExpr

  datatype DeclMappings = DeclMappings(typeMap: map<TypeDecl, STypeDecl>, functionMap: map<Function, STypedDeclaration>)
  {
    method Type2SType(typ: Type) returns (r: SType) {
      r := Type2STypeWithMap(typ, typeMap);
    }

    static method Type2STypeWithMap(typ: Type, typeMap: map<TypeDecl, STypeDecl>) returns (r: SType) {
      match typ
      case BoolType => r := SBool;
      case IntType | TagType => r := SInt;
      case UserType(decl) =>
        expect decl in typeMap, decl.Name;
        var sTypeDecl := typeMap[decl];
        r := SUserType(sTypeDecl);
    }
  }

  datatype Incarnations = Incarnations(nextSequenceCount: map<string, nat>, m: map<Variable, SConstant>, declMap: DeclMappings)
  {
    static function Empty(declMap: DeclMappings): Incarnations {
      Incarnations(map[], map[], declMap)
    }

    function DeclMap(): DeclMappings {
      declMap
    }

    method Print(header: string := "") {
      print "==== Incarnations: ", header, "\n";

      print "  nextSequenceCount:\n";
      var ss := nextSequenceCount.Keys;
      while ss != {} {
        var s :| s in ss;
        ss := ss - {s};
        print "    ", s, " := ", nextSequenceCount[s], "\n";
      }

      print "  m:\n";
      var vv := m.Keys;
      while vv != {} {
        var v: Variable :| v in vv;
        vv := vv - {v};
        print "    ", v.name, " := ", m[v].name, "\n";
      }
    }

    method Type2SType(typ: Type) returns (r: SType) {
      r := declMap.Type2SType(typ);
    }

    function Variables(): set<Variable> {
      m.Keys
    }

    method Get(v: Variable) returns (r: SConstant) {
      expect v in m, v.name;
      r := m[v];
    }

    // `Set` is intended to be used only during custom initializations of an Incarnations.
    function Set(v: Variable, sv: SConstant): Incarnations {
      this.(nextSequenceCount := map[v.name := 0] + nextSequenceCount, m := m[v := sv])
    }

    // `Set` is intended to be used only during custom initializations of an Incarnations.
    function CreateSubMap(customMap: map<Variable, SConstant>): Incarnations {
      Incarnations(nextSequenceCount, customMap, declMap)
    }

    method ReserveAux(v: Variable) returns (next: nat, x: SConstant) {
      var name := v.name;
      if name in nextSequenceCount.Keys {
        var n := nextSequenceCount[name];
        name := name + "%" + Int2String(n);
        next := n + 1;
      } else {
        next := 0;
      }
      var typ := declMap.Type2SType(v.typ);
      x := new SConstant(name, typ);
    }

    method Reserve(v: Variable) returns (incarnations: Incarnations, x: SConstant) {
      var nextSequenceNumber;
      nextSequenceNumber, x := ReserveAux(v);
      incarnations := this.(nextSequenceCount := nextSequenceCount[v.name := nextSequenceNumber]);
    }

    method Update(v: Variable) returns (incarnations: Incarnations, x: SConstant) {
      var nextSequenceNumber;
      nextSequenceNumber, x := ReserveAux(v);
      incarnations := this.(nextSequenceCount := nextSequenceCount[v.name := nextSequenceNumber], m := m[v := x]);
    }

    function DomainRestrict(variables: set<Variable>): Incarnations {
      var m' := map v <- m.Keys | v in variables :: m[v];
      this.(m := m')
    }

    method CreateForBoundVariables(expr: Expr) returns (r: Incarnations)
      decreases expr
    {
      r := this;
      match expr
      case BLiteral(_) =>
      case ILiteral(_) =>
      case CustomLiteral(_, _) =>
      case IdExpr(_) =>
      case OperatorExpr(_, args) =>
        for i := 0 to |args| {
          r := r.CreateForBoundVariables(args[i]);
        }
      case FunctionCallExpr(_, args) =>
        for i := 0 to |args| {
          r := r.CreateForBoundVariables(args[i]);
        }
      case LabeledExpr(_, body) =>
        r := r.CreateForBoundVariables(body);
      case LetExpr(v, rhs, body) =>
        r := r.CreateForBoundVariables(rhs);
        expect v !in r.m.Keys;
        var sVar;
        r, sVar := Update(v);
        r := r.CreateForBoundVariables(body);
      case QuantifierExpr(_, vv, patterns, body) =>
        expect forall v <- vv :: v !in r.m.Keys;
        r := this;
        for i := 0 to |vv| {
          var sVar;
          r, sVar := r.Update(vv[i]);
        }
        for i := 0 to |patterns| {
          var pattern := patterns[i];
          for j := 0 to |pattern.exprs| {
            r := r.CreateForBoundVariables(pattern.exprs[j]);
          }
        }
        r := r.CreateForBoundVariables(body);
      case ClosureExpr(_, _, _, _) =>
        // Closures must be elaborated before verification
        expect false;
    }

    // Create SConstant's for the bound variables in "expr" and then substitute these and the other incarnations
    // into "expr".
    method REval(expr: Expr) returns (r: RSolvers.RExpr)
      requires expr.WellFormed()
    {
      var incarnations := CreateForBoundVariables(expr);
      r := incarnations.Substitute(expr);
    }

    method SubstituteVariable(v: Variable) returns (r: SConstant) {
      expect v in m, v.name;
      r := m[v];
    }

    method Substitute(expr: Expr) returns (r: RSolvers.RExpr)
      requires expr.WellFormed()
      decreases expr, 0
    {
      match expr
      case BLiteral(value) => r := RExpr.Boolean(value);
      case ILiteral(value) => r := RExpr.Integer(value);
      case CustomLiteral(s, typ) =>
        var st := declMap.Type2SType(typ);
        r := RExpr.CustomLiteral(s, st);
      case IdExpr(v) =>
        var sv := SubstituteVariable(v);
        r := RExpr.Id(sv);
      case OperatorExpr(op, args) =>
        var rArgs := SubstituteList(args);
        match op {
          case IfThenElse =>
            r := RExpr.IfThenElse(rArgs[0], rArgs[1], rArgs[2]);
          case Neq =>
            var eq := RExpr.FuncAppl(RExpr.Operator2ROperator(Operator.Eq), rArgs);
            r := RExpr.FuncAppl(RExpr.Operator2ROperator(Operator.LogicalNot), [eq]);
          case _ =>
            r := RExpr.FuncAppl(RExpr.Operator2ROperator(op), rArgs);
        }
      case FunctionCallExpr(func, args) =>
        var rArgs := SubstituteList(args);
        expect func in declMap.functionMap, func.Name;
        var f := declMap.functionMap[func];
        r := RExpr.FuncAppl(RSolvers.UserDefinedFunction(func, f, None), rArgs);
      case LabeledExpr(_, body) =>
        // TODO: do something with the label
        r := Substitute(body);
      case LetExpr(v, rhs, body) =>
        expect v in m, v.name;
        var sVar := m[v];
        var rhsValue := Substitute(rhs);
        var bodyValue := Substitute(body);
        r := RExpr.LetExpr(sVar, rhsValue, bodyValue);
      case QuantifierExpr(univ, vv, patterns, body) =>
        expect forall v <- vv :: v in m, "quantifier-vars";
        var sVars := SeqMap(vv, v requires v in m => m[v]);
        var trs := SubstitutePatterns(patterns);
        var b := Substitute(body);
        r := RExpr.QuantifierExpr(univ, sVars, trs, b);
      case ClosureExpr(_, _, _, _) =>
        // Closures must be elaborated before verification
        expect false, "closure-not-elaborated";
        r := RExpr.Boolean(false); // unreachable
    }

    method SubstituteList(exprs: seq<Expr>) returns (r: seq<RSolvers.RExpr>)
      requires forall expr <- exprs :: expr.WellFormed()
      ensures |r| == |exprs|
      decreases exprs, 1
    {
      r := [];
      for i := 0 to |exprs|
        invariant |r| == i
      {
        var item := Substitute(exprs[i]);
        r := r + [item];
      }
    }

    method SubstitutePatterns(patterns: seq<Pattern>) returns (r: seq<RSolvers.RPattern>)
      requires forall tr <- patterns :: tr.WellFormed()
      decreases patterns, 0
    {
      r := [];
      for i := 0 to |patterns| {
        assert patterns[i].WellFormed();
        var exprs := SubstituteList(patterns[i].exprs);
        r := r + [RSolvers.RPattern(exprs)];
      }
    }
  }

}