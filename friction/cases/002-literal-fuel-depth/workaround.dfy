datatype T = V | C(c: real) | Add(a: T, b: T)

function ev(t: T, x: real): real {
  match t
  case V => x
  case C(c) => c
  case Add(a,b) => ev(a,x)+ev(b,x)
}

function one(): real { 1.0 }

// Assert the value of every subterm deeper than two levels.
lemma LiteralDepth3(x: real) ensures ev(Add(V,Add(C(1.0),V)),x) == 2.0*x+1.0 {
  assert ev(Add(C(1.0),V),x) == 1.0+x;
}
lemma CallDepth3(x: real) ensures ev(Add(V,Add(C(one()),V)),x) == 2.0*x+1.0 {
  assert ev(Add(C(one()),V),x) == 1.0+x;
}
