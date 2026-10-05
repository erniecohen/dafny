// Nested unused lets must not admit the enclosing function's own contract.
ghost function Nested(i: int): int
  ensures var a := 1; var b := 2; Nested(i) == 0
{ 1 }

// A used let retains the allowance after substituting its bound variable.
ghost function Used(i: int): int
  ensures var x := i; Used(x) == 0
{ 1 }

// Exact pattern lets follow the same permission traversal.
ghost function Pattern(i: int): int
  ensures var (x, unused) := (i, 1); Pattern(x) == 0
{ 1 }
