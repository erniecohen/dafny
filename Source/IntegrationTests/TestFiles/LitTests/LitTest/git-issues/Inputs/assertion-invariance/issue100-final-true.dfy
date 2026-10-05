type Ord<!T(!new)> = (T,T)->bool
ghost predicate iChain<T(!new)>(o:Ord<T>,c:nat->T) { forall n:nat :: o(c(n),c(n+1)) }
ghost predicate wf<T(!new)>(o:Ord<T>) { forall c:nat->T :: !iChain(o,c) }
type WfO<!T(!new)> = o:Ord<T> | wf(o) witness *
ghost predicate inductiveP<T(!new)>(p:T->bool,o:WfO<T>) { forall t:T | (forall t0:T | o(t,t0) :: p(t0)) :: p(t) }
ghost function iRegress<T(!new)>(p:T->bool,o:WfO<T>,t:T,n:nat):(r:T) requires inductiveP(p,o) && !p(t) decreases n {
    if n == 0 then t else var t1 := iRegress(p,o,t,n-1); var r :| o(t1,r) && !p(r); r
}
ghost function rf<T(!new)>(p:T->bool,o:WfO<T>,t:T):(r:nat->T) requires inductiveP(p,o) && !p(t) ensures iChain(o,r) {
    (n:nat) => iRegress(p,o,t,n)
}
lemma wfi<T(!new)>(p:T->bool,o:WfO<T>,t:T) requires inductiveP(p,o) ensures forall t :: p(t) {
    forall t ensures p(t) { if !p(t) { var ch := rf(p,o,t); assert wf(o); }} 
    assert true;
}

