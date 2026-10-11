ghost function F(i: int): int { i }

lemma TerminalRevealScope(i: int, b: bool)
  ensures F(i) == i
{
  hide *;
  if b {
    calc {
      F(i);
      == { { reveal F; } }
      i;
    }
  } else {
    calc {
      F(i);
      == { { reveal F; } }
      i;
    }
  }
}

lemma TerminalForallRevealScope()
  ensures forall i: int :: F(i) == i
{
  hide *;
  forall i: int ensures F(i) == i {
    calc {
      F(i);
      == { { reveal F; } }
      i;
    }
  }
}
