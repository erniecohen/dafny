module FactoryControl {
  import opened Std.Actions
  import opened Std.Producers
  import opened Std.Termination
  import opened Std.Wrappers
  class {:termination_closed} Factory extends OutputterOfNewProducers<int, int> {


    ghost predicate Valid()
      decreases {:global} (allowance), 6
      reads this, Repr
      ensures Valid() ==> this in Repr
      ensures Valid() ==> ValidHistory(history)
      ensures Valid() ==> producedAllowanceCeiling <= allowance
      decreases Repr, 0
    {
      && allowance == 3 && producedAllowanceCeiling == 2
      &&       this in Repr
    }

    twostate predicate ValidChange()
      decreases {:global} (allowance), 14
      reads this, Repr
      ensures ValidChange() ==> old(Valid()) && Valid()
      ensures ValidChange() ==> fresh(Repr - old(Repr))
      ensures ValidChange() ==> old(history) <= history
    {
      && fresh(Repr - old(Repr))
      && old(Valid())
      && Valid()
      && old(history) <= history
    }

    twostate lemma ValidImpliesValidChange()
      decreases {:global} (allowance), 18
      requires old(Valid())
      requires unchanged(old(Repr))
      ensures ValidChange()
    {}

    constructor()
      decreases {:global} 4 as ORDINAL, 128
      ensures allowance == 3 && producedAllowanceCeiling == 2
      ensures Valid() && history == [] && fresh(Repr)
    {
      allowance := 3;
      producedAllowanceCeiling := 2;
      Repr := {this};
      history := [];
    }

    ghost function MaxProduced(): TerminationMetric
      decreases {:global} allowance, 10
    {
      TMNat(1)
    }

    ghost predicate ValidHistory(history: seq<(int, Producer<int>)>)
      decreases {:global} (allowance), 3
      ensures ValidHistory(history) ==> 1 <= allowance
      decreases Repr
    {
      && allowance == 3 && producedAllowanceCeiling == 2
      &&       true
    }
    ghost predicate ValidInput(history: seq<(int, Producer<int>)>, next: int)
      decreases {:global} (allowance), 4
      decreases Repr
    {
      && allowance == 3 && producedAllowanceCeiling == 2
      &&       true
    }

    ghost function Decreases(t: int): ORDINAL
      decreases {:global} (allowance), 12
      reads Reads(t)
    {
      0
    }

    method Invoke(i: int) returns (o: Producer<int>)
      decreases {:global} allowance, 20
      requires Requires(i)
      reads Repr
      modifies Modifies(i)
      decreases Decreases(i), 0
      ensures Ensures(i, o)
      ensures OutputFresh(o)
    {
      var empty := new EmptyProducer<int>();
      o := empty;
      UpdateHistory(i, o);
      reveal TerminationMetric.Ordinal();
    }

    lemma AnyInputIsValid(history: seq<(int, Producer<int>)>, next: int)
      decreases {:global} (allowance), 10
      requires Valid()
      requires Action().ValidHistory(history)
      ensures Action().ValidInput(history, next)
    {}
  }


  method {:isolate_assertions} Use() {
    var source := new SeqReader([7]);
    var factory := new Factory();
    var mapped := new MappedProducerOfNewProducers(source, factory);
    var flattened := new FlattenedProducer(mapped);
    var output := flattened.Next();
    assert flattened.Valid();
    // CONTROL
  }
}
