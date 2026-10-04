module Types {
  import opened Std.Wrappers
  import opened Basics

  type TypeName = string
  const BoolTypeName := "bool"
  const IntTypeName := "int"
  const RealTypeName := "real"
  const TagTypeName := "tag"
  const BuiltInTypes: set<TypeName> := {BoolTypeName, IntTypeName, RealTypeName, TagTypeName}
  const MaximumBitvectorWidth := 4096
  const MaximumBitvectorBits := 4194304
  type BitvectorWidth = w: int | 0 < w <= MaximumBitvectorWidth witness 1

  function BitvectorTypeName(width: int): string { "#bv" + Int2String(width) }
  predicate ReservedTypeName(name: string) { name in BuiltInTypes || "#bv" <= name }
  predicate IsBuiltInType(name: string) { name in BuiltInTypes || ParseBitvectorWidth(name).Some? }

  // Parsing never raises a power or allocates an object proportional to an unchecked width.
  function ParseBitvectorWidth(name: string): Option<BitvectorWidth> {
    if !("#bv" <= name) || |name| < 4 || |name| > 7 || name[3] == '0' then None else
    var width := ReadWidthDigits(name[3..], 0);
    if 0 < width <= MaximumBitvectorWidth then Some(width) else None
  }
  function ReadWidthDigits(chars: string, value: int): int
    decreases |chars|
  {
    if chars == "" then value else
    if chars[0] < '0' || '9' < chars[0] then -1 else
    var next := value * 10 + (chars[0] as int - '0' as int);
    if next > MaximumBitvectorWidth then -1 else ReadWidthDigits(chars[1..], next)
  }

  // Squaring gives logarithmic recursion depth in the positive, bounded word width.
  function WordBound(width: nat): nat
    ensures WordBound(width) > 0
  {
    if width == 0 then 1 else
    var half := WordBound(width / 2);
    half * half * (if width % 2 == 0 then 1 else 2)
  }
  predicate BitvectorLiteralValid(value: int, width: int) {
    0 < width <= MaximumBitvectorWidth && 0 <= value < WordBound(width)
  }
  datatype Word = Word(value: int, width: BitvectorWidth) {
    predicate Valid() { 0 <= value < WordBound(width) }
  }
  type CanonicalWord = word: Word | word.Valid() witness Word(0, 1)
}
