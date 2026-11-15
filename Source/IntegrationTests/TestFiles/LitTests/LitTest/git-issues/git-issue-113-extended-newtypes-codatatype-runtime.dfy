// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Bare<T(0)> = Stream<T>

function Repeat<T>(x: T): Wrapped<T> {
  Cons(x, Repeat(x) as Stream<T>) as Wrapped<T>
}

function Count(x: int): Wrapped<int> {
  Cons(x, Count(x + 1) as Stream<int>) as Wrapped<int>
}

function {:abstemious} Copy<T>(s: Wrapped<T>): Wrapped<T> {
  Cons(s.head, Copy(s.tail as Wrapped<T>) as Stream<T>) as Wrapped<T>
}

function {:abstemious} MatchCopy<T>(s: Wrapped<T>): Wrapped<T> {
  match s case Cons(h, t) => Cons(h, MatchCopy(t as Wrapped<T>) as Stream<T>) as Wrapped<T>
}

codatatype OuterStream = Outer(stream: Wrapped<int>, tail: OuterStream)
newtype WrappedOuter = OuterStream witness *
function OuterRepeat(x: int): WrappedOuter {
  Outer(Repeat(x), OuterRepeat(x) as OuterStream) as WrappedOuter
}

codatatype Duo<A,B> = Duo(left: A, right: B, tail: Duo<A,B>)
newtype BareFlip<X(0),Y(0)> = Duo<Y,X>

method Main() {
  var repeat := Repeat(7);
  print repeat.head, " ", repeat.tail.head, " ", repeat.tail.tail.head, "\n";
  var count := Count(4);
  print count.head, " ", count.tail.head, " ", count.tail.tail.head, "\n";
  var copied := Copy(count);
  var matched := MatchCopy(count);
  print copied.head, " ", copied.tail.head, " ", matched.head, " ", matched.tail.head, "\n";
  var outer := OuterRepeat(9);
  print outer.stream.head, " ", outer.tail.stream.head, " ", outer.tail.tail.stream.head, "\n";
  var zeros: Bare<int>;
  print zeros.head, " ", zeros.tail.head, " ", zeros.tail.tail.head, "\n";
  var flags: Bare<bool>;
  print flags.head, " ", flags.tail.head, "\n";
  var permuted: BareFlip<int,bool>;
  print permuted.left, " ", permuted.right, " ", permuted.tail.left, " ", permuted.tail.right, "\n";
}
