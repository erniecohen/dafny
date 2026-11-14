// Comparisons between two values of traits that are not reference types.  Without
// --trait-rank-decreases these crash the verifier (see the regression's notes), so this file runs
// only with the option on.
trait T {
  function f(): nat decreases this
}

datatype D extends T = Leaf | Node(child: T) {
  function f(): nat decreases this {
    if Leaf? then 0 else child.f()
  }
}

// A trait component that does not change, then a decreasing number.
function H(x: T, n: nat): nat decreases x, n {
  if n == 0 then 0 else H(x, n - 1)
}

// A trait value whose dynamic type is D: its field is below it.
function Down(x: T): nat decreases x {
  if x is D then
    var d := x as D;
    if d.Node? then 1 + Down(d.child) else 0
  else 0
}

// False controls.
function Swap(x: T, y: T): nat decreases x {
  Swap(y, x)
}

function Same(x: T): nat decreases x {
  Same(x)
}

function Up(x: T): nat decreases x {
  Up(Node(x))
}
