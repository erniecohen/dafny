// Translation-only probe consumed by the structured encoding audit.
type Encoding82Empty = x: int | false witness *
type Encoding82Tiny = x: int | x == 0 witness 0

ghost function Encoding82Finite(n: int): set<int> {
  set i: int | 0 <= i < n
}

ghost function Encoding82SimpleISet(n: int): iset<int> {
  iset i: int | n <= i
}

ghost function Encoding82EmptyOwn(): set<int> {
  set x: Encoding82Empty | 0 <= x < 1 :: 0
}

ghost function Encoding82EmptyOuter(z: Encoding82Empty): set<int> {
  set i: int | i == 0
}

ghost function Encoding82EmptyQuantifier(): bool {
  forall z: Encoding82Empty :: z in (set i: int | i == z)
}

ghost function Encoding82Guarded(n: int): bool {
  n != 0 ==> n in (set i: int | i == n)
}

ghost function Encoding82Typed(n: int): map<int, int> {
  map x: Encoding82Tiny | true :: n + x := x
}

ghost function Encoding82Generic<T(!new)>(a: T): map<int, T> {
  map x: T | x == a :: 0 := x
}

ghost function Encoding82Map(n: int): map<int, int> {
  map x: int | x == n :: 0 := x
}

ghost function Encoding82Tuple(n: int, b: bool): map<int, (int, bool)> {
  map x: int, y: bool | x == n && y == b :: 0 := (x, y)
}

ghost function Encoding82IMap(n: int): imap<int, int> {
  imap x: int | x == n :: 0 := x
}

ghost function Encoding82Nested(n: int): set<int> {
  set i: int | i == 0 && (map j: int | j == n + i :: 0 := j)[0] == n
}

ghost function Encoding82KeyOnly(n: int): set<int> {
  ((k: int) => map j: int | j == 0 :: k := j)(n).Keys
}

ghost function Encoding82Lambda(): (object?) -> set<object?> {
  (o: object?) => set i: int | i == 0 :: o
}
