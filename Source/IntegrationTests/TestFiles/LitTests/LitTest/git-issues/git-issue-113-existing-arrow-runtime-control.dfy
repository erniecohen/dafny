// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

type Total = int -> int
type Partial = int --> int
type General = int ~> int
type Nullary = () -> int
type Binary = (int, int) -> int
type Endo<!T> = T -> T
type Produce<+T> = () -> T

class Cell {
  var value: int
  constructor(value: int)
    ensures this.value == value
  {
    this.value := value;
  }
}

method Main() {
  var total := ((x: int) => x + 1) as Total;
  print total(41), "\n";
  var partial := ((x: int) requires 0 <= x => 2 * x) as Partial;
  print partial(21), "\n";
  var cell := new Cell(42);
  var general := ((x: int) reads cell => cell.value + x) as General;
  print general(0), "\n";
  var zero := (() => 42) as Nullary;
  print zero(), "\n";
  var binary := ((x: int, y: int) => x + y) as Binary;
  print binary(20, 22), "\n";
  var endo := ((x: int) => x) as Endo<int>;
  print endo(42), "\n";
  var produce := (() => 42) as Produce<int>;
  print produce(), "\n";
}
