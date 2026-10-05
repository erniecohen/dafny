// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function Repeat<T>(x:T):Wrapped<T> { (Cons(x, (Repeat(x) as Stream<T>)) as Wrapped<T>) }
function RepeatOther<T>(x:T):Wrapped<T> { (Cons(x, (RepeatOther(x) as Stream<T>)) as Wrapped<T>) }
function TrueRepeat<T>(x:T):Trivial<T> { Cons(x, TrueRepeat(x) as Stream<T>) as Trivial<T> }
function {:abstemious} Copy<T>(s:Wrapped<T>):Wrapped<T> { (Cons(s.head, (Copy((s.tail as Wrapped<T>)) as Stream<T>)) as Wrapped<T>) }
function {:abstemious} MatchCopy<T>(s:Wrapped<T>):Wrapped<T> { match s case Cons(h,t) => (Cons(h, (MatchCopy((t as Wrapped<T>)) as Stream<T>)) as Wrapped<T>) }
function Count(x:int):Wrapped<int> { (Cons(x, (Count(x+1) as Stream<int>)) as Wrapped<int>) }
codatatype Duo<A,B> = Duo(left:A,right:B,tail:Duo<A,B>)
newtype Flip<X,Y> = Duo<Y,X> witness *
newtype Deep<T> = Flip<seq<T>,bool> witness *
function Permuted<T>(xs:seq<T>):Deep<T> { (Duo(false,xs,(Permuted(xs) as Flip<seq<T>,bool>) as Duo<bool,seq<T>>) as Flip<seq<T>,bool>) as Deep<T> }
codatatype OuterStream = Outer(stream:Wrapped<int>,tail:OuterStream)
newtype WrappedOuter = OuterStream witness *
function OuterRepeat(x:int):WrappedOuter { Outer(Repeat(x),OuterRepeat(x) as OuterStream) as WrappedOuter }
lemma RepeatObservations<T>(x:T)
  ensures Repeat(x).head == x && Repeat(x).tail.head == x
{}
lemma CopyObservations<T>(s:Wrapped<T>)
  ensures Copy(s).head == s.head && Copy(s).tail.head == s.tail.head
  ensures MatchCopy(s).head == s.head && MatchCopy(s).tail.head == s.tail.head
{
  var inputTail := s.tail as Wrapped<T>;
  var copiedTail := Copy(inputTail);
  var matchedTail := MatchCopy(inputTail);
  assert Copy(s).tail == (copiedTail as Stream<T>);
  assert copiedTail.head == s.tail.head;
  assert MatchCopy(s).tail == (matchedTail as Stream<T>);
  assert matchedTail.head == s.tail.head;
}
lemma CountObservations(x:int)
  ensures Count(x).head == x && Count(x).tail.head == x+1
{
  var next := Count(x+1);
  assert Count(x).tail == (next as Stream<int>);
  assert next.head == x+1;
}
lemma PermutedObservations<T>(xs:seq<T>)
  ensures !Permuted(xs).left && Permuted(xs).right == xs
  ensures Permuted(xs).tail.right == xs
{}
lemma OuterObservations(x:int)
  ensures OuterRepeat(x).stream.head == x
  ensures OuterRepeat(x).tail.stream.head == x
{}
greatest lemma RepeatEquality(x:int)
  ensures Repeat(x) == RepeatOther(x)
{ RepeatEquality(x); }
