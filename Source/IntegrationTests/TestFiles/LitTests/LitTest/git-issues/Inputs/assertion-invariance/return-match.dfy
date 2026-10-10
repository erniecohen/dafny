datatype D = D(i:int) type S = i:int | i>=0 witness 0 ghost function L(d:D):S requires d.i>=0 { match d case D(i) => i }
