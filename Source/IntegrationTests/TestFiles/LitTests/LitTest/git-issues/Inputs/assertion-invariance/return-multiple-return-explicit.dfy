type S = i:int | i>=0 witness 0 method L(i:int,b:bool) returns (r:S) requires i>=0 { if b { assert i>=0; return i; } assert i>=0; return i; }
