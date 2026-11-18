// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
newtype F = f: (int -> int) | true witness (x: int) => x
datatype Cell = Cell(value: int)
newtype CellView = Cell witness Cell(0)
codatatype Stream = More(head: int, tail: Stream)
newtype StreamView = Stream witness *
