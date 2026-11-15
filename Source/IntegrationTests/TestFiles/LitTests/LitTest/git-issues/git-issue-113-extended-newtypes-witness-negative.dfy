// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// All witnesses have the declared base carrier; each fails the target predicate.
datatype Cell = Cell(value: int)
newtype PositiveCell = c: Cell | c.value > 0 witness Cell(0)
newtype ZeroOrdinal = o: ORDINAL | o == 0 witness 1
newtype ZeroAtZero = f: int -> int | f(0) == 0 witness ((x: int) => x + 1)
