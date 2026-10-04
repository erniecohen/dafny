module Types {
  // Types

  type TypeName = string

  const BoolTypeName := "bool"
  const IntTypeName := "int"
  const RealTypeName := "real"
  const TagTypeName := "tag"
  const BuiltInTypes: set<TypeName> := {BoolTypeName, IntTypeName, RealTypeName, TagTypeName}
}