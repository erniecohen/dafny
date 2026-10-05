// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"

type Positive = n:int | 0 < n witness 1
codatatype Stream = Cons(head:Positive,tail:Stream)
type StreamN = Stream
type Observe = int -> Positive
function Repeat(n:Positive):StreamN { Cons(n,Repeat(n) as Stream) as StreamN }
ghost function FiniteObserver(s:StreamN):Observe {
  ((x:int) => (map i:int | i == x :: 0 := s.head)[0]) as Observe
}
lemma ObserveContract(n:Positive,x:int)
  ensures FiniteObserver(Repeat(n))(x) == n
{
  var s := Repeat(n);
  assert s.head == n;
  var observer := FiniteObserver(s);
  assert observer(x) == n;
}

lemma LiveControl() { ObserveContract(1,0); assert FiniteObserver(Repeat(1))(0) == 1; assert false; }
