// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Newtype constraints in an acyclic result-field graph are implicit result
// obligations. A co-call must not assume them while introducing those fields.

codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Never<T> = s:Stream<T> | false witness *
function Zeros():Stream<int> { Cons(0, Zeros()) }

datatype Box<T> = Box(value:T)
codatatype Outer<T> = Outer(tail:Outer<T>,field:T)
function BadGeneric():Outer<Never<int>> {
  Outer(BadGeneric(), Zeros() as Never<int>)
}
function BadNested():Outer<Box<Never<int>>> {
  Outer(BadNested(), Box(Zeros() as Never<int>))
}
function BadCollection():Outer<seq<Never<int>>> {
  Outer(BadCollection(), [Zeros() as Never<int>])
}

// A visible trivial newtype result still carries its base field memberships.
type NeverInt = n:int | false witness *
codatatype PlainOuter = PlainOuter(tail:PlainOuter,field:NeverInt)
newtype WrappedPlainOuter = PlainOuter witness *
function BadWrappedSubsetField():WrappedPlainOuter {
  PlainOuter(BadWrappedSubsetField() as PlainOuter, 0 as NeverInt) as WrappedPlainOuter
}

lemma NonVacuity() {
  var a := BadGeneric();
  var b := BadNested();
  var c := BadCollection();
  var d := BadWrappedSubsetField();
  assert false;
}
