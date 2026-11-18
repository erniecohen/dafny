module Client {
  import opened Std.Actions
  import opened Std.Producers
  import opened Std.Consumers
  import opened Std.Wrappers

  method {:isolate_assertions} FunctionFamily() {
    var callback := bounded<ORDINAL, (int -> int)>.certify(0, x => x + 1);
    var action := new FunctionAction(callback);
    ghost var total := new TotalFunctionActionProof(action, callback);
    assert total.Valid();
    var value := action.Invoke(1);
    assert action.Valid();

  }

  method {:isolate_assertions} FunctionalFamily() {
    var step := bounded<ORDINAL, (int -> (int, int))>.certify(0, s => (s + 1, s));
    var functional := new FunctionalIProducer(0, step);
    var value := functional.Next();
    assert functional.Valid();

  }

  method {:isolate_assertions} FilterFamily() {
    var reader := new SeqReader([1, 2, 3]);
    var filtering :=  bounded<ORDINAL, (int -> bool)>.certify(0, x => x % 2 == 1);
    var filtered := new FilteredProducer(reader, filtering);
    var item := filtered.Next();
    assert filtered.Valid();

  }

  method {:isolate_assertions} FoldFamily() {
    var addition := bounded<ORDINAL, ((int, int) -> int)>.certify(0, (x, y) => x + y);
    var folding := new FoldingConsumer(0, addition);
    var unit := folding.Invoke(2);
    assert folding.Valid();
  }

  method {:print} Main() {
    FunctionFamily();
    FunctionalFamily();
    FilterFamily();
    FoldFamily();
    // CONTROL
    print "five families exercised\n";
  }
}
