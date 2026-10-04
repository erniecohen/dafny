trait Value { function Get(): int }
datatype D extends Value = D(n: int) { function Get(): int { n } }
lemma Dispatch(d: D) { var v: Value := d; assert v.Get() == d.n; }
