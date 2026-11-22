module Client {
  import opened Std.Actions
  import opened Std.Producers
  import opened Std.Consumers
  import opened Std.Wrappers

  datatype Stored<F> = Stored(callback: F)

  function Return<F>(stored: Stored<F>): F { stored.callback }

  method {:isolate_assertions} FunctionFamily() {
    var callback := Return(Stored(bounded<ORDINAL, (int -> int)>.certify(0, x => x + 1)));
    var action := new FunctionAction(callback);
    ghost var total := new TotalFunctionActionProof(action, callback);
    assert total.Valid();
    assert action.f.bound == 0 && action.f(1) == 2;
    assert total.f.bound == 0 && total.f(1) == 2;
    var value := action.Invoke(1);
    assert action.Valid();
    // FAMILY CONTROL
  }

  method {:isolate_assertions} FunctionalFamily() {
    var step := Return(Stored(bounded<ORDINAL, (int -> (int, int))>.certify(0, s => (s + 1, s))));
    var functional := new FunctionalIProducer(0, step);
    assert functional.stepFn.bound < functional.allowance;
    assert step(0) == (1, 0);
    var value := functional.Next();
    assert functional.Valid();
    // FAMILY CONTROL
  }

  method {:isolate_assertions} FilterFamily() {
    var reader := new SeqReader([1, 2, 3]);
    var filtering := Return(Stored(bounded<ORDINAL, (int -> bool)>.certify(0, x => x % 2 == 1)));
    var filtered := new FilteredProducer(reader, filtering);
    assert filtered.filter.bound < filtered.allowance;
    assert filtering(1);
    var item := filtered.Next();
    assert filtered.Valid();
    // FAMILY CONTROL
  }

  method {:isolate_assertions} FoldFamily() {
    var addition := Return(Stored(bounded<ORDINAL, ((int, int) -> int)>.certify(0, (x, y) => x + y)));
    var folding := new FoldingConsumer(0, addition);
    assert folding.f.bound < folding.allowance;
    assert addition(2, 3) == 5;
    var unit := folding.Invoke(2);
    assert folding.Valid();
    // FAMILY CONTROL
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
