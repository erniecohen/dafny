class Cell { var value: int }
method Update(c: Cell, a: array<int>)
 requires a.Length > 0
 modifies c, a
 ensures c.value == old(c.value) + 1 && a[0] == old(a[0]) + 1
{ c.value := c.value + 1; a[0] := a[0] + 1; }
