// Membership in the image set through an introduction and an elimination
// lemma; the law proofs never reveal the comprehension.
ghost predicate Down(s: iset<real>) {
  forall a, b {:trigger a in s, b in s} | a in s && b < a :: b in s
}
ghost function up(s: iset<real>, q: real): real
ghost predicate Open(s: iset<real>) {
  forall q {:trigger up(s,q)} | q in s :: up(s,q) in s && q < up(s,q)
}
opaque ghost function add(x: iset<real>, y: iset<real>): iset<real> {
  iset a, b | a in x && b in y :: a + b
}
ghost function zero(): iset<real> { iset q: real {:nowarn} | q < 0.0 }

lemma AddIntro(x: iset<real>, y: iset<real>, a: real, b: real)
  requires a in x && b in y ensures a + b in add(x,y)
{ reveal add(); }
lemma AddElim(x: iset<real>, y: iset<real>, q: real) returns (a: real, b: real)
  requires q in add(x,y) ensures a in x && b in y && q == a + b
{ reveal add(); a, b :| a in x && b in y && q == a + b; }

lemma AddZero(x: iset<real>) requires Down(x) && Open(x) ensures add(x,zero()) == x {
  forall q | q in add(x,zero()) ensures q in x {
    var a, b := AddElim(x, zero(), q);
  }
  forall q | q in x ensures q in add(x,zero()) {
    var p := up(x,q);
    AddIntro(x, zero(), p, q-p);
  }
}
