// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

datatype Box<T> = Box(value: T)
newtype WrappedInt = Box<int> witness Box(0)
datatype Outer = Outer(value: WrappedInt)

datatype Pair<A,B> = Pair(left: A, right: B)
newtype Flip<X,Y> = p: Pair<Y,X> | true witness *
newtype Deep<T> = p: Flip<seq<T>,bool> | true witness *
datatype GenericContainer<X,Y> = GC(pair: Flip<X,Y>)
newtype WrappedGeneric<X,Y> = GenericContainer<X,Y> witness *

datatype Cell = Cell(value: int)
newtype PositiveCell = c: Cell | c.value > 0 witness Cell(1)
datatype Container = Container(cell: PositiveCell, count: int)
newtype WrappedContainer = Container witness Container(Cell(1) as PositiveCell, 0)

newtype PositiveTuple = p: (bool, int) | p.1 > 0 witness (false, 1)
newtype BarePair = Pair<bool,int>
datatype Choice = First | Second(value: int)
newtype BareChoice = Choice
newtype GenericBare<T(0)> = Box<T>

function Nested(c: WrappedContainer): int {
  match c case Container(Cell(v), count) => v + count
}
function NestedLet(c: WrappedContainer): int {
  var Container(Cell(v), count) := c;
  v + count
}
function Whole(n: PositiveCell): PositiveCell {
  match n case Cell(0) => n case whole => whole
}
function NestedPermutation(g: WrappedGeneric<seq<int>, bool>): (bool, seq<int>) {
  match g case GC(Pair(b, s)) => (b, s)
}
function NestedPermutationLet(g: WrappedGeneric<seq<int>, bool>): (bool, seq<int>) {
  var GC(Pair(b, s)) := g;
  (b, s)
}

method Main() {
  var x := Outer(Box(7) as WrappedInt);
  print x.value.value, "\n";
  var flip := Pair(false, [42]) as Flip<seq<int>, bool>;
  var deep := flip as Deep<int>;
  print deep.left, " ", deep.right[0], "\n";
  var g := GC(flip) as WrappedGeneric<seq<int>, bool>;
  var pm := NestedPermutation(g);
  var pl := NestedPermutationLet(g);
  print pm.0, " ", pm.1[0], " ", pl.0, " ", pl.1[0], "\n";
  var c := Container(Cell(5) as PositiveCell, 3) as WrappedContainer;
  print Nested(c), " ", NestedLet(c), "\n";
  var t := (false, 3) as PositiveTuple;
  var (b, i) := t;
  print b, " ", i, "\n";
  var bare: BarePair;
  print bare.left, " ", bare.right, "\n";
  var choice: BareChoice;
  print choice, "\n";
  var generic: GenericBare<bool>;
  print generic.value, "\n";
  var positive := Cell(8) as PositiveCell;
  var updated := positive.(value := Whole(positive).value + 1);
  print updated.value, "\n";
}
