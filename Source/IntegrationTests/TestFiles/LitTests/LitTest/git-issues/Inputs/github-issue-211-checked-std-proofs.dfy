module ProofWitnesses {
  import opened Std.Actions
  import opened Std.Producers
  import opened Std.CallPermissions
  import Std.Collections.Seq
  import opened Std.Wrappers

  class {:termination_closed} One extends IProducer<int> {
    constructor()
      decreases {:global} 1 as ORDINAL, 128
      ensures allowance == 1 && Valid() && history == [] && fresh(Repr)
    { allowance := 1; Repr := {this}; history := []; }

    ghost predicate Valid()
      decreases {:global} allowance, 6
      reads this, Repr
      ensures Valid() ==> this in Repr && ValidHistory(history)
      decreases Repr, 0
    { allowance == 1 && this in Repr && ValidHistory(history) }

    ghost predicate ValidHistory(h: seq<((), int)>)
      decreases {:global} allowance, 3
      ensures ValidHistory(h) ==> 1 <= allowance
      decreases Repr
    { allowance == 1 && (h == [] || h == [((), 7)]) }

    ghost predicate ValidInput(h: seq<((), int)>, next: ())
      decreases {:global} allowance, 4
      requires ValidHistory(h)
      decreases Repr
    { h == [] }

    twostate predicate ValidChange()
      decreases {:global} allowance, 14
      reads this, Repr
      ensures ValidChange() ==> old(Valid()) && Valid() && fresh(Repr - old(Repr))
      ensures ValidChange() ==> old(history) <= history
    { old(Valid()) && Valid() && fresh(Repr - old(Repr)) && old(history) <= history }

    twostate lemma ValidImpliesValidChange()
      decreases {:global} allowance, 18
      requires old(Valid()) && unchanged(old(Repr))
      ensures ValidChange()
    {}

    ghost function Decreases(i: ()): ORDINAL
      decreases {:global} allowance, 12
      reads Reads(i)
    { 0 }

    method Invoke(i: ()) returns (o: int)
      decreases {:global} allowance, 20
      requires Requires(i)
      reads Reads(i)
      modifies Modifies(i)
      decreases Decreases(i), 0
      ensures Ensures(i, o)
    { assert Requires(i); assert this in Reads(i); o := 7; UpdateHistory(i, o); }
  }

  class {:termination_closed} SingletonProof extends ProducesSetProof<int> {
    ghost const {:termination_closed} subject: One
    ghost constructor(subject: One)
      decreases {:global} 2 as ORDINAL, 128
      requires subject.allowance == 1
      ensures Valid() && Action() == subject
    { allowance := 2; this.subject := subject; }

    ghost predicate Valid()
      decreases {:global} allowance, 6
    { allowance == 2 && subject.allowance == 1 }

    ghost function Action(): (result: Action<(), int>)
      decreases {:global} allowance, 8
      requires Valid()
      ensures result.allowance <= allowance
    { subject }

    ghost function Set(): set<int>
      decreases {:global} allowance, 7
      requires Valid()
    { {7} }

    lemma ProducesSet(h: seq<((), int)>)
      decreases {:global} allowance, 40
      requires Valid() && Action().ValidHistory(h)
      ensures |h| <= |Set()|
      ensures |h| < |Set()| ==> Action().ValidInput(h, ())
      ensures Seq.HasNoDuplicates(OutputsOf(h))
      ensures Seq.ToSet(OutputsOf(h)) <= Set()
    { assert h == [] || h == [((), 7)]; }
  }

  class {:termination_closed} Composition extends ActionCompositionProof<int, int, int> {
    ghost const {:termination_closed} first: FunctionAction<int, int>
    ghost const {:termination_closed} second: FunctionAction<int, int>
    ghost const {:termination_closed} callback: bounded<ORDINAL, (int -> int)>
    ghost constructor(first: FunctionAction<int, int>, second: FunctionAction<int, int>,
                      callback: bounded<ORDINAL, (int -> int)>)
      decreases {:global} 2 as ORDINAL, 128
      requires first.allowance == second.allowance == 1
      requires first.f.erase == second.f.erase == callback.erase
      ensures Valid() && FirstAction() == first && SecondAction() == second
    { allowance := 2; this.first := first; this.second := second; this.callback := callback; }

    ghost predicate Valid()
      decreases {:global} allowance, 6
    { allowance == 2 && first.allowance == second.allowance == 1 &&
      first.f.erase == second.f.erase == callback.erase }

    ghost function FirstAction(): (result: Action<int, int>)
      decreases {:global} allowance, 8
      requires Valid()
      ensures result.allowance <= allowance
    { first }
    ghost function SecondAction(): (result: Action<int, int>)
      decreases {:global} allowance, 8
      requires Valid()
      ensures result.allowance <= allowance
    { second }
    ghost predicate ComposedValidInput(h: seq<(int, int)>, next: int)
      decreases {:global} allowance, 7
      requires Valid()
    { true }
    ghost predicate ComposedValidHistory(h: seq<(int, int)>): (result: bool)
      decreases {:global} allowance, 7
      requires Valid()
      ensures h == [] ==> result
    { true }
    lemma CanInvokeFirst(h: seq<(int, int)>, composed: seq<(int, int)>, next: int)
      decreases {:global} allowance, 40
      requires Valid() && FirstAction().ValidHistory(h)
      requires ComposedValidInput(composed, next) && InputsOf(h) == InputsOf(composed)
      ensures FirstAction().ValidInput(h, next)
    {}
    lemma CanInvokeSecond(h: seq<(int, int)>, composed: seq<(int, int)>, next: int, middle: int)
      decreases {:global} allowance, 40
      requires Valid() && SecondAction().ValidHistory(h) && OutputsOf(h) == OutputsOf(composed)
      ensures SecondAction().ValidInput(h, middle)
    {}
    lemma CanReturn(h: seq<(int, int)>, h2: seq<(int, int)>, composed: seq<(int, int)>)
      decreases {:global} allowance, 40
      requires Valid() && FirstAction().ValidHistory(h) && SecondAction().ValidHistory(h2)
      ensures ComposedValidHistory(composed)
    {}
  }

  class {:termination_closed} EmptyProof extends ProducerOfSetProof<int> {
    ghost const {:termination_closed} subject: EmptyProducer<int>
    ghost constructor(subject: EmptyProducer<int>)
      decreases {:global} 2 as ORDINAL, 128
      requires subject.allowance == 1
      ensures Valid() && Producer() == subject
    { allowance := 2; this.subject := subject; }

    ghost predicate Valid()
      decreases {:global} allowance, 6
    { allowance == 2 && subject.allowance == 1 }
    ghost function Producer(): (result: Producer<int>)
      decreases {:global} allowance, 8
      requires Valid()
      ensures result.allowance <= allowance
    { subject }
    ghost function Set(): set<int>
      decreases {:global} allowance, 7
      requires Valid()
    { {} }
    lemma ProducesSet(h: seq<((), Option<int>)>)
      decreases {:global} allowance, 40
      requires Valid() && Producer().ValidHistory(h)
      ensures
        var produced := ProducedOf(OutputsOf(h));
        && Seq.HasNoDuplicates(produced)
        && Seq.ToSet(produced) <= Set()
        && (!Seq.AllClosed(OutputsOf(h), IsSome) ==> Seq.ToSet(produced) == Set())
    {
      assert subject.ValidOutputs(OutputsOf(h));
      assert |OutputsOf(h)| == 0 || OutputsOf(h)[0].None?;
      assert ProducedOf(OutputsOf(h)) == [];
    }
  }

  method {:isolate_assertions} Use() {
    var one := new One();
    ghost var proof := new SingletonProof(one);
    proof.ProducesSet([]);
    var value := one.Next();
    proof.ProducesSet(one.history);
    var identity := bounded<ORDINAL, (int -> int)>.certify(0, x => x);
    var a := new FunctionAction(identity);
    var b := new FunctionAction(identity);
    ghost var composition := new Composition(a, b, identity);
    composition.CanInvokeFirst([], [], 3);
    composition.CanInvokeSecond([], [], 3, 3);
    composition.CanReturn([], [], []);
    var empty := new EmptyProducer<int>();
    ghost var emptyProof := new EmptyProof(empty);
    emptyProof.ProducesSet([]);
    var end := empty.Next();
    emptyProof.ProducesSet(empty.history);
    // CONTROL
  }
}
