type S = i:int | i>=0 witness 0 ghost function L(i:int):S requires i>=0 { var v := if i%2==0 then i else i+1; assert v>=0; v }
