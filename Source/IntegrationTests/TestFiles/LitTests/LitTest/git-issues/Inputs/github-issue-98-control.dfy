// Vacuity control for github-issue-98.dfy: its program, with `assert false` at the end.
type Ord<!T(!new)> = (T,T)->bool
ghost predicate iChain<T(!new)>(o:Ord<T>,c:nat->T) { forall n:nat :: o(c(n),c(n+1)) }
ghost predicate wf<T(!new)>(o:Ord<T>) { forall c:nat->T :: !iChain(o,c) }
type WfO<!T(!new)> = o:Ord<T> | wf(o) witness *
method test<S(!new),T(!new)>(ot:WfO<T>,c:nat->(S,T),j:nat) {
    var c1:nat->T := (i:nat) => c(i+j).1;
    forall c1:nat->T {:trigger iChain(ot,c1)} ensures !iChain(ot,c1) {}
    assert !iChain(ot,c1);
    assert false;
}
