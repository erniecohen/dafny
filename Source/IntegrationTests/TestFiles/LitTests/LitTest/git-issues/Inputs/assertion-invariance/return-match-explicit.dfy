datatype D = D(i:int) type S = i:int | i>=0 witness 0 ghost function L(d:D):S requires d.i>=0 { var v := match d case D(i) => i; assert v>=0; v }
