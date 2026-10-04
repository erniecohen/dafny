// Private draft; not registered or executed. Expected stdout: seven lines of 42.
// Intended flags: --type-system-refresh=true --general-newtypes=true --extended-newtype-bases
// Run applicable arrow-capable backends: C#, Java, JavaScript, Go, Python.

newtype Total = int -> int witness ((x: int) => x)
newtype Partial = int --> int witness ((x: int) => x)
newtype General = int ~> int witness ((x: int) => x)
newtype Nullary = () -> int witness (() => 0)
newtype Binary = (int, int) -> int witness ((x: int, y: int) => x + y)
newtype Endo<!T> = T -> T witness *
newtype Produce<+T> = () -> T witness *

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
