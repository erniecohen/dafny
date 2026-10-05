module TypeResolver {
  import opened Std.Wrappers
  import opened Ast
  import opened Types

  export
    provides ResolveType
    provides Ast, Types, Wrappers

  method ResolveType(typename: string, typeMap: map<string, TypeDecl>) returns (r: Result<Type, string>)
    ensures r.Success? ==> IsBuiltInType(typename) || typename in typeMap
  {
    if typename == BoolTypeName {
      return Success(BoolType);
    } else if typename == IntTypeName {
      return Success(IntType);
    } else if typename == RealTypeName {
      return Success(RealType);
    } else if typename == TagTypeName {
      return Success(TagType);
    }

    var width := ParseBitvectorWidth(typename);
    if width.Some? { return Success(BitvectorType(width.value)); }
    if "#bv" <= typename { return Failure("invalid or excessive bitvector width: " + typename); }
    if typename !in typeMap {
      return Failure("unknown type: " + typename);
    }
    return Success(UserType(typeMap[typename]));
  }
}