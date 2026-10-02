// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-traits=datatype --general-newtypes=true

trait V { function Tag(): int }
datatype Box extends V = Box(n: int) { function Tag(): int { n } }
trait Indexed<T> { function Value(): T }
datatype IndexedBox<T> extends Indexed<T> = IndexedBox(x: T) { function Value(): T { x } }
method Main() {
  var v: V := Box(3);
  assert v is Box;
  assert (v as Box).n == 3;
  assert v.Tag() == 3;
  print v.Tag(), " ";
  var indexed: Indexed<int> := IndexedBox(7);
  assert indexed is IndexedBox<int>;
  assert (indexed as IndexedBox<int>).x == 7;
  assert indexed.Value() == 7;
  print indexed.Value(), "\n";
}
