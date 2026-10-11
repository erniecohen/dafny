ghost predicate P(i: int) requires i != 0 { 10/i > 0 }
type S = i: int | i != 0 && P(i) witness 1
ghost function F(): S { 0 }
