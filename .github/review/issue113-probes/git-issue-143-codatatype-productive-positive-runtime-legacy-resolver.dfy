// Private unexecuted #143 controls; no #113 option or newtype declaration.
// Proposed expectation: verifies with refreshed and legacy resolution.
codatatype NatStream = N(head: nat, tail: NatStream)
function Count(n: nat): NatStream { N(n, Count(n + 1)) }
function At(s: NatStream, k: nat): nat
  decreases k
{
  if k == 0 then s.head else At(s.tail, k - 1)
}
lemma CountAt(n: nat, k: nat)
  ensures At(Count(n), k) == n + k
  decreases k
{
  if k != 0 { CountAt(n + 1, k - 1); }
}
lemma NatObservation(s: NatStream, k: nat)
  ensures At(s, k) >= 0
{}

type Even = x: int | x % 2 == 0 witness 0
codatatype EvenStream = E(tail: EvenStream, head: Even)
function Evens(n: int): EvenStream { E(Evens(n + 1), (2 * n) as Even) }
function EvenAt(s: EvenStream, k: nat): int
  decreases k
{
  if k == 0 then s.head else EvenAt(s.tail, k - 1)
}
lemma EvensAt(n: int, k: nat)
  ensures EvenAt(Evens(n), k) == 2 * (n + k)
  decreases k
{
  if k != 0 { EvensAt(n + 1, k - 1); }
}

codatatype Choice = Done | More(head: nat, tail: Choice)
function Choices(n: nat): Choice { More(n, Choices(n + 1)) }
lemma ChoiceHead(n: nat)
  ensures Choices(n).More? && Choices(n).head == n
{}

function MutualA(n: nat): NatStream { N(n, MutualB(n + 1)) }
function MutualB(n: nat): NatStream { N(n, MutualA(n + 1)) }
lemma MutualHeads(n: nat)
  ensures MutualA(n).head == n && MutualB(n).head == n
{}

codatatype GenericStream<T> = G(head: T, tail: GenericStream<T>)
function Repeat<T>(value: T): GenericStream<T> { G(value, Repeat(value)) }
function GenericAt<T>(s: GenericStream<T>, k: nat): T
  decreases k
{
  if k == 0 then s.head else GenericAt(s.tail, k - 1)
}
lemma RepeatAt<T>(value: T, k: nat)
  ensures GenericAt(Repeat(value), k) == value
  decreases k
{
  if k != 0 { RepeatAt(value, k - 1); }
}

type NonnegativeHead = s: NatStream | s.head >= 0 witness *
function CheckedSubset(n: nat): NonnegativeHead { Count(n) }
lemma CheckedSubsetHead(n: nat)
  ensures CheckedSubset(n).head == n
{}

method Main() {
  var a := At(Count(4), 7);
  var b := EvenAt(Evens(-3), 5);
  print a, " ", b, "\n";
}
