// Cuts as downward-closed sets of reals with no largest element. Addition
// is the image of + on the two sets; the law is x + 0 == x.
ghost predicate Down(s: iset<real>) {
  forall a, b {:trigger a in s, b in s} | a in s && b < a :: b in s
}
// The witness of openness is a function, so the hypothesis cannot loop.
ghost function up(s: iset<real>, q: real): real
ghost predicate Open(s: iset<real>) {
  forall q {:trigger up(s,q)} | q in s :: up(s,q) in s && q < up(s,q)
}
ghost function add(x: iset<real>, y: iset<real>): iset<real> {
  iset a, b | a in x && b in y :: a + b
}
ghost function zero(): iset<real> { iset q: real {:nowarn} | q < 0.0 }

// Each inclusion on its own.
lemma Sub(x: iset<real>) requires Down(x) && Open(x)
  ensures forall q | q in add(x,zero()) :: q in x
{}
lemma Sup(x: iset<real>) requires Down(x) && Open(x)
  ensures forall q | q in x :: q in add(x,zero())
{
  forall q | q in x ensures q in add(x,zero()) {
    var p := up(x,q);
    assert p in x && q-p in zero() && q == p + (q-p);
  }
}
// Combined through the two lemmas.
lemma AddZeroSplit(x: iset<real>) requires Down(x) && Open(x) ensures add(x,zero()) == x {
  Sub(x); Sup(x);
}
// The same proof in one lemma: the hint for one inclusion, the other automatic.
lemma AddZero(x: iset<real>) requires Down(x) && Open(x) ensures add(x,zero()) == x {
  forall q | q in x ensures q in add(x,zero()) {
    var p := up(x,q);
    assert p in x && q-p in zero() && q == p + (q-p);
  }
}
