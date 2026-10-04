// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
function Repeat<T>(x:T):Stream<T> { (Cons(x, (Repeat(x)))) }
function RepeatOther<T>(x:T):Stream<T> { (Cons(x, (RepeatOther(x)))) }
function TrueRepeat<T>(x:T):Stream<T> { Cons(x,TrueRepeat(x)) }
function {:abstemious} Copy<T>(s:Stream<T>):Stream<T> { (Cons(s.head, (Copy((s.tail))))) }
function {:abstemious} MatchCopy<T>(s:Stream<T>):Stream<T> { match s case Cons(h,t) => (Cons(h, (MatchCopy((t))))) }
function Count(x:int):Stream<int> { (Cons(x, (Count(x+1)))) }
codatatype Duo<A,B> = Duo(left:A,right:B,tail:Duo<A,B>)
function Permuted<T>(xs:seq<T>):Duo<bool,seq<T>> { Duo(false,xs,Permuted(xs)) }
codatatype OuterStream = Outer(stream:Stream<int>,tail:OuterStream)
function OuterRepeat(x:int):OuterStream { Outer(Repeat(x),OuterRepeat(x)) }
lemma RepeatObservations<T>(x:T)
  ensures Repeat(x).head == x && Repeat(x).tail.head == x
{}
lemma CopyObservations<T>(s:Stream<T>)
  ensures Copy(s).head == s.head && Copy(s).tail.head == s.tail.head
  ensures MatchCopy(s).head == s.head && MatchCopy(s).tail.head == s.tail.head
{
  var copiedTail := Copy(s.tail);
  var matchedTail := MatchCopy(s.tail);
  assert Copy(s).tail == copiedTail;
  assert copiedTail.head == s.tail.head;
  assert MatchCopy(s).tail == matchedTail;
  assert matchedTail.head == s.tail.head;
}
lemma CountObservations(x:int)
  ensures Count(x).head == x && Count(x).tail.head == x+1
{
  var next := Count(x+1);
  assert Count(x).tail == next;
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
