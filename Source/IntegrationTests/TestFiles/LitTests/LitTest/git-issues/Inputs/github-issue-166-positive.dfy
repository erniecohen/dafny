ghost function Nested(i: int): int
  ensures var a := 1; var b := 2; Nested(i) == 1
{ 1 }

ghost function Used(i: int): int
  ensures var x := i; Used(x) == 1
{ 1 }

ghost function Pattern(i: int): int
  ensures var (x, unused) := (i, 1); Pattern(x) == 1
{ 1 }

lemma CheckContracts(i: int) {
  assert Nested(i) == 1;
  assert Used(i) == 1;
  assert Pattern(i) == 1;
}
