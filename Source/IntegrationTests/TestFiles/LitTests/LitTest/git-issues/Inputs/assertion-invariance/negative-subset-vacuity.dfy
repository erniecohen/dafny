type Positive = x: int | x > 0 witness 1
lemma L() { var x: Positive := 1; assert false; }
