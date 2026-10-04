function F(x: int): int { x + 1 }
lemma Visibility(x: int) { hide F; reveal F; assert F(x) == x + 1; }
