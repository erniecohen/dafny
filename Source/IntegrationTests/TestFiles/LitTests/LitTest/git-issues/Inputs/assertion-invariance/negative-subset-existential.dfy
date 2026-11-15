type S = i:int | exists n:int :: n>=0 && i==n witness 0 ghost function F():S { -1 }
