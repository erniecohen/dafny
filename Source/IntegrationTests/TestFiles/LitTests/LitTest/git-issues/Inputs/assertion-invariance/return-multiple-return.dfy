type S = i:int | i>=0 witness 0 method L(i:int,b:bool) returns (r:S) requires i>=0 { if b { return i; } return i; }
