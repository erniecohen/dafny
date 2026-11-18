// Under the hidden --legacy-no-modular-termination, "decreases {:global} n" is an ordinary decreases clause.
method Loop(n: nat) decreases {:global} n { Loop(n); }      // error: n does not decrease
method Down(n: nat) decreases {:global} n { if n > 0 { Down(n - 1); } }
