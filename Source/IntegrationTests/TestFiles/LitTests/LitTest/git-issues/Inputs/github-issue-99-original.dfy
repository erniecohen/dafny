type Ord<!T(!new)> = (T,T)->bool
ghost predicate iChain<T(!new)>(o:Ord<T>,c:nat->T) { forall n:nat :: o(c(n),c(n+1)) }
ghost predicate wf<T(!new)>(o:Ord<T>) { forall c:nat->T :: !iChain(o,c) }
type WfO<!T(!new)> = o:Ord<T> | wf(o) witness *
function lex<S(!new),T(!new)>(os:WfO<S>,ot:WfO<T>):WfO<(S,T)> {
    var r := (o:(S,T),o1:(S,T)) => os(o.0,o1.0);
    forall c,r | iChain<(S,T)>(r,c) ensures false {}
    r
}
