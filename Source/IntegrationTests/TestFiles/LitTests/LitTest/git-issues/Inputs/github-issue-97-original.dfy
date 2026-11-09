predicate p(x:int)
const f := (x:int) requires p(x) => x
lemma test(x:int) ensures f.requires(x) ==> p(x) {}
