// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked native rotations at power-of-two word widths. Requires --integer-bitwise.
include "IntegerBitwiseNativeShifts.dfy"

module Std.Arithmetic.IntegerBitwiseNativeRotationArithmetic {
  import R=IntegerBitwise
  lemma {:induction false} LegalCount(n:nat,k:nat)
    requires 0<n
    requires k<=n
    ensures k<n ==> k%n==k
    ensures k==n ==> k%n==0
  { if k<n { R.Quotient(k,n,0,k); } else { R.Quotient(k,n,1,0); } }
  lemma {:induction false} {:isolate_assertions} Parts(a:int,p:int,q:int)
    requires 0<p
    requires 0<q
    requires 0<=a<p*q
    ensures 0 <= (a*p)%(p*q)+a/q < p*q
  {
    R.Quotient(a,p*q,0,a);
    R.DivProduct(a,q,p);
    assert a/q/p==0;
    assert 0<=a/q<p;
    R.MulMod(a,p,q);
    R.ProductBounds(a/q,p,a%q,q);
  }
  lemma {:induction false} {:isolate_assertions} LeftMeaning(n:nat,a:int,k:nat)
    requires 0<n
    requires k<=n
    requires 0<=a<R.P(n)
    ensures 0<=(a*R.P(k))%R.P(n)+a/R.P(n-k)<R.P(n)
    ensures (a*R.P(k))%R.P(n)+a/R.P(n-k)==R.rolNat(n,a,k)
  {
    LegalCount(n,k); R.PowerZero(); R.NormId(n,a); R.PowerSum(k,n-k);
    Parts(a,R.P(k),R.P(n-k)); reveal R.rolNat();
    if k==n { R.Quotient(a*R.P(n),R.P(n),a,0); }
  }
  lemma {:induction false} {:isolate_assertions} RightMeaning(n:nat,a:int,k:nat)
    requires 0<n
    requires k<=n
    requires 0<=a<R.P(n)
    ensures 0<=a/R.P(k)+(a*R.P(n-k))%R.P(n)<R.P(n)
    ensures a/R.P(k)+(a*R.P(n-k))%R.P(n)==R.rorNat(n,a,k)
  {
    LegalCount(n,k); R.PowerZero(); R.NormId(n,a); R.PowerSum(n-k,k);
    Parts(a,R.P(n-k),R.P(k)); R.MulMod(a,R.P(n-k),R.P(k)); reveal R.rorNat();
    if k==n { R.Quotient(a,1,a,0); R.Quotient(a,R.P(n),0,a); }
  }
  lemma {:induction false} Finish(z:int,l:int,r:int,m:int)
    requires 0<m
    requires 0<=l+r<m
    requires z==(l+r)%m
    ensures z==l+r
  { R.Quotient(l+r,m,0,l+r); }
}

module Std.Arithmetic.IntegerBitwiseNativeRotations8 {
  import B = IntegerBitwiseNative8
  import S = IntegerBitwiseNativeShifts8
  import C = IntegerBitwiseNativeShiftCount4
  import A = IntegerBitwiseNativeRotationArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  // Keep count conversion separate from the native rotation identity.
  lemma {:induction false} WideCount(k:bv4)
    ensures (k as bv8) as int==k as int
  {}
  lemma {:induction false} EmbedCount(k:bv4)
    ensures (k as nat) as bv8==k as bv8
  { WideCount(k); B.Inverse8(k as bv8); }
  lemma {:induction false} ComplementCount(k:bv4)
    requires k<=8
    ensures (8-k) as int==8-k as int
  {}

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv8,k:bv4)
    requires k<=8
    ensures x.RotateLeft(k as nat)==(x << k)+(x >> (8-k))
  {
    EmbedCount(k);
    if k<=4 {
      if k<=2 {
        if k<=1 {
          if k<=0 {
            assert k==0;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
          } else {
            assert k==1;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
          }
        } else {
          assert k==2;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        }
      } else {
        if k<=3 {
          assert k==3;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        } else {
          assert k==4;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        }
      }
    } else {
      if k<=6 {
        if k<=5 {
          assert k==5;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        } else {
          assert k==6;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        }
      } else {
        if k<=7 {
          assert k==7;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        } else {
          assert k==8;
          assert x.RotateLeft(k as nat)==(x << k)+(x >> (8-k));
        }
      }
    }
  }

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv8,k:bv4)
    requires k<=8
    ensures x.RotateRight(k as nat)==(x >> k)+(x << (8-k))
  {
    EmbedCount(k);
    if k<=4 {
      if k<=2 {
        if k<=1 {
          if k<=0 {
            assert k==0;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
          } else {
            assert k==1;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
          }
        } else {
          assert k==2;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        }
      } else {
        if k<=3 {
          assert k==3;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        } else {
          assert k==4;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        }
      }
    } else {
      if k<=6 {
        if k<=5 {
          assert k==5;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        } else {
          assert k==6;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        }
      } else {
        if k<=7 {
          assert k==7;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        } else {
          assert k==8;
          assert x.RotateRight(k as nat)==(x >> k)+(x << (8-k));
        }
      }
    }
  }

  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv8,k:bv4,z:bv8)
    requires k<=8
    requires z==x.RotateLeft(k as nat)
    ensures z as int==R.rolNat(8,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.LeftMeaning(8,x as int,k as nat);
    ComplementCount(k); NativeLeft(x,k);
    var l,r:=x << k,x >> (8-k);
    assert z==l+r;
    S.LeftWitness(x,k,l); S.RightWitness(x,8-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,256);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rolNat(8,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} LeftRotate(x:bv8,k:nat)
    requires k<=8
    ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(8,x as int,k)
  {
    C.RoundTrip4(k); S.CountBound(k as bv4);
    assert (k as bv4)<=8;
    assert x.RotateLeft(k)==x.RotateLeft((k as bv4) as nat);
    LeftWitness(x,k as bv4,x.RotateLeft((k as bv4) as nat));
    F.LeftRotate(8,x as int,k);
  }

  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv8,k:bv4,z:bv8)
    requires k<=8
    requires z==x.RotateRight(k as nat)
    ensures z as int==R.rorNat(8,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.RightMeaning(8,x as int,k as nat);
    ComplementCount(k); NativeRight(x,k);
    var l,r:=x >> k,x << (8-k);
    assert z==l+r;
    S.RightWitness(x,k,l); S.LeftWitness(x,8-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,256);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rorNat(8,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} RightRotate(x:bv8,k:nat)
    requires k<=8
    ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(8,x as int,k)
  {
    C.RoundTrip4(k); S.CountBound(k as bv4);
    assert (k as bv4)<=8;
    assert x.RotateRight(k)==x.RotateRight((k as bv4) as nat);
    RightWitness(x,k as bv4,x.RotateRight((k as bv4) as nat));
    F.RightRotate(8,x as int,k);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeRotations16 {
  import B = IntegerBitwiseNative16
  import S = IntegerBitwiseNativeShifts16
  import C = IntegerBitwiseNativeShiftCount5
  import A = IntegerBitwiseNativeRotationArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  // Keep count conversion separate from the native rotation identity.
  lemma {:induction false} WideCount(k:bv5)
    ensures (k as bv16) as int==k as int
  {}
  lemma {:induction false} EmbedCount(k:bv5)
    ensures (k as nat) as bv16==k as bv16
  { WideCount(k); B.Inverse16(k as bv16); }
  lemma {:induction false} ComplementCount(k:bv5)
    requires k<=16
    ensures (16-k) as int==16-k as int
  {}

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv16,k:bv5)
    requires k<=16
    ensures x.RotateLeft(k as nat)==(x << k)+(x >> (16-k))
  {
    EmbedCount(k);
    if k<=8 {
      if k<=4 {
        if k<=2 {
          if k<=1 {
            if k<=0 {
              assert k==0;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
            } else {
              assert k==1;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
            }
          } else {
            assert k==2;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        } else {
          if k<=3 {
            assert k==3;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==4;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        }
      } else {
        if k<=6 {
          if k<=5 {
            assert k==5;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==6;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        } else {
          if k<=7 {
            assert k==7;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==8;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        }
      }
    } else {
      if k<=12 {
        if k<=10 {
          if k<=9 {
            assert k==9;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==10;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        } else {
          if k<=11 {
            assert k==11;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==12;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        }
      } else {
        if k<=14 {
          if k<=13 {
            assert k==13;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==14;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        } else {
          if k<=15 {
            assert k==15;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          } else {
            assert k==16;
            assert x.RotateLeft(k as nat)==(x << k)+(x >> (16-k));
          }
        }
      }
    }
  }

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv16,k:bv5)
    requires k<=16
    ensures x.RotateRight(k as nat)==(x >> k)+(x << (16-k))
  {
    EmbedCount(k);
    if k<=8 {
      if k<=4 {
        if k<=2 {
          if k<=1 {
            if k<=0 {
              assert k==0;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
            } else {
              assert k==1;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
            }
          } else {
            assert k==2;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        } else {
          if k<=3 {
            assert k==3;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==4;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        }
      } else {
        if k<=6 {
          if k<=5 {
            assert k==5;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==6;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        } else {
          if k<=7 {
            assert k==7;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==8;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        }
      }
    } else {
      if k<=12 {
        if k<=10 {
          if k<=9 {
            assert k==9;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==10;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        } else {
          if k<=11 {
            assert k==11;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==12;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        }
      } else {
        if k<=14 {
          if k<=13 {
            assert k==13;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==14;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        } else {
          if k<=15 {
            assert k==15;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          } else {
            assert k==16;
            assert x.RotateRight(k as nat)==(x >> k)+(x << (16-k));
          }
        }
      }
    }
  }

  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv16,k:bv5,z:bv16)
    requires k<=16
    requires z==x.RotateLeft(k as nat)
    ensures z as int==R.rolNat(16,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.LeftMeaning(16,x as int,k as nat);
    ComplementCount(k); NativeLeft(x,k);
    var l,r:=x << k,x >> (16-k);
    assert z==l+r;
    S.LeftWitness(x,k,l); S.RightWitness(x,16-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,65536);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rolNat(16,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} LeftRotate(x:bv16,k:nat)
    requires k<=16
    ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(16,x as int,k)
  {
    C.RoundTrip5(k); S.CountBound(k as bv5);
    assert (k as bv5)<=16;
    assert x.RotateLeft(k)==x.RotateLeft((k as bv5) as nat);
    LeftWitness(x,k as bv5,x.RotateLeft((k as bv5) as nat));
    F.LeftRotate(16,x as int,k);
  }

  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv16,k:bv5,z:bv16)
    requires k<=16
    requires z==x.RotateRight(k as nat)
    ensures z as int==R.rorNat(16,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.RightMeaning(16,x as int,k as nat);
    ComplementCount(k); NativeRight(x,k);
    var l,r:=x >> k,x << (16-k);
    assert z==l+r;
    S.RightWitness(x,k,l); S.LeftWitness(x,16-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,65536);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rorNat(16,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} RightRotate(x:bv16,k:nat)
    requires k<=16
    ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(16,x as int,k)
  {
    C.RoundTrip5(k); S.CountBound(k as bv5);
    assert (k as bv5)<=16;
    assert x.RotateRight(k)==x.RotateRight((k as bv5) as nat);
    RightWitness(x,k as bv5,x.RotateRight((k as bv5) as nat));
    F.RightRotate(16,x as int,k);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeRotations32 {
  import B = IntegerBitwiseNative32
  import S = IntegerBitwiseNativeShifts32
  import C = IntegerBitwiseNativeShiftCount6
  import A = IntegerBitwiseNativeRotationArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  // Keep count conversion separate from the native rotation identity.
  lemma {:induction false} WideCount(k:bv6)
    ensures (k as bv32) as int==k as int
  {}
  lemma {:induction false} EmbedCount(k:bv6)
    ensures (k as nat) as bv32==k as bv32
  { WideCount(k); B.Inverse32(k as bv32); }
  lemma {:induction false} ComplementCount(k:bv6)
    requires k<=32
    ensures (32-k) as int==32-k as int
  {}

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv32,k:bv6)
    requires k<=32
    ensures x.RotateLeft(k as nat)==(x << k)+(x >> (32-k))
  {
    EmbedCount(k);
    if k<=16 {
      if k<=8 {
        if k<=4 {
          if k<=2 {
            if k<=1 {
              if k<=0 {
                assert k==0;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
              } else {
                assert k==1;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
              }
            } else {
              assert k==2;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=3 {
              assert k==3;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==4;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        } else {
          if k<=6 {
            if k<=5 {
              assert k==5;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==6;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=7 {
              assert k==7;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==8;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        }
      } else {
        if k<=12 {
          if k<=10 {
            if k<=9 {
              assert k==9;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==10;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=11 {
              assert k==11;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==12;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        } else {
          if k<=14 {
            if k<=13 {
              assert k==13;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==14;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=15 {
              assert k==15;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==16;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        }
      }
    } else {
      if k<=24 {
        if k<=20 {
          if k<=18 {
            if k<=17 {
              assert k==17;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==18;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=19 {
              assert k==19;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==20;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        } else {
          if k<=22 {
            if k<=21 {
              assert k==21;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==22;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=23 {
              assert k==23;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==24;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        }
      } else {
        if k<=28 {
          if k<=26 {
            if k<=25 {
              assert k==25;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==26;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=27 {
              assert k==27;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==28;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        } else {
          if k<=30 {
            if k<=29 {
              assert k==29;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==30;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          } else {
            if k<=31 {
              assert k==31;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            } else {
              assert k==32;
              assert x.RotateLeft(k as nat)==(x << k)+(x >> (32-k));
            }
          }
        }
      }
    }
  }

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv32,k:bv6)
    requires k<=32
    ensures x.RotateRight(k as nat)==(x >> k)+(x << (32-k))
  {
    EmbedCount(k);
    if k<=16 {
      if k<=8 {
        if k<=4 {
          if k<=2 {
            if k<=1 {
              if k<=0 {
                assert k==0;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
              } else {
                assert k==1;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
              }
            } else {
              assert k==2;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=3 {
              assert k==3;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==4;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        } else {
          if k<=6 {
            if k<=5 {
              assert k==5;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==6;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=7 {
              assert k==7;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==8;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        }
      } else {
        if k<=12 {
          if k<=10 {
            if k<=9 {
              assert k==9;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==10;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=11 {
              assert k==11;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==12;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        } else {
          if k<=14 {
            if k<=13 {
              assert k==13;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==14;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=15 {
              assert k==15;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==16;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        }
      }
    } else {
      if k<=24 {
        if k<=20 {
          if k<=18 {
            if k<=17 {
              assert k==17;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==18;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=19 {
              assert k==19;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==20;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        } else {
          if k<=22 {
            if k<=21 {
              assert k==21;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==22;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=23 {
              assert k==23;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==24;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        }
      } else {
        if k<=28 {
          if k<=26 {
            if k<=25 {
              assert k==25;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==26;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=27 {
              assert k==27;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==28;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        } else {
          if k<=30 {
            if k<=29 {
              assert k==29;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==30;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          } else {
            if k<=31 {
              assert k==31;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            } else {
              assert k==32;
              assert x.RotateRight(k as nat)==(x >> k)+(x << (32-k));
            }
          }
        }
      }
    }
  }

  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv32,k:bv6,z:bv32)
    requires k<=32
    requires z==x.RotateLeft(k as nat)
    ensures z as int==R.rolNat(32,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.LeftMeaning(32,x as int,k as nat);
    ComplementCount(k); NativeLeft(x,k);
    var l,r:=x << k,x >> (32-k);
    assert z==l+r;
    S.LeftWitness(x,k,l); S.RightWitness(x,32-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,4294967296);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rolNat(32,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} LeftRotate(x:bv32,k:nat)
    requires k<=32
    ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(32,x as int,k)
  {
    C.RoundTrip6(k); S.CountBound(k as bv6);
    assert (k as bv6)<=32;
    assert x.RotateLeft(k)==x.RotateLeft((k as bv6) as nat);
    LeftWitness(x,k as bv6,x.RotateLeft((k as bv6) as nat));
    F.LeftRotate(32,x as int,k);
  }

  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv32,k:bv6,z:bv32)
    requires k<=32
    requires z==x.RotateRight(k as nat)
    ensures z as int==R.rorNat(32,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.RightMeaning(32,x as int,k as nat);
    ComplementCount(k); NativeRight(x,k);
    var l,r:=x >> k,x << (32-k);
    assert z==l+r;
    S.RightWitness(x,k,l); S.LeftWitness(x,32-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,4294967296);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rorNat(32,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} RightRotate(x:bv32,k:nat)
    requires k<=32
    ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(32,x as int,k)
  {
    C.RoundTrip6(k); S.CountBound(k as bv6);
    assert (k as bv6)<=32;
    assert x.RotateRight(k)==x.RotateRight((k as bv6) as nat);
    RightWitness(x,k as bv6,x.RotateRight((k as bv6) as nat));
    F.RightRotate(32,x as int,k);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeRotations64 {
  import B = IntegerBitwiseNative64
  import S = IntegerBitwiseNativeShifts64
  import C = IntegerBitwiseNativeShiftCount7
  import A = IntegerBitwiseNativeRotationArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  // Keep count conversion separate from the native rotation identity.
  lemma {:induction false} WideCount(k:bv7)
    ensures (k as bv64) as int==k as int
  {}
  lemma {:induction false} EmbedCount(k:bv7)
    ensures (k as nat) as bv64==k as bv64
  { WideCount(k); B.Inverse64(k as bv64); }
  lemma {:induction false} ComplementCount(k:bv7)
    requires k<=64
    ensures (64-k) as int==64-k as int
  {}

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv64,k:bv7)
    requires k<=64
    ensures x.RotateLeft(k as nat)==(x << k)+(x >> (64-k))
  {
    EmbedCount(k);
    if k<=32 {
      if k<=16 {
        if k<=8 {
          if k<=4 {
            if k<=2 {
              if k<=1 {
                if k<=0 {
                  assert k==0;
                  assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
                } else {
                  assert k==1;
                  assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
                }
              } else {
                assert k==2;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=3 {
                assert k==3;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==4;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=6 {
              if k<=5 {
                assert k==5;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==6;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=7 {
                assert k==7;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==8;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        } else {
          if k<=12 {
            if k<=10 {
              if k<=9 {
                assert k==9;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==10;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=11 {
                assert k==11;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==12;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=14 {
              if k<=13 {
                assert k==13;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==14;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=15 {
                assert k==15;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==16;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        }
      } else {
        if k<=24 {
          if k<=20 {
            if k<=18 {
              if k<=17 {
                assert k==17;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==18;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=19 {
                assert k==19;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==20;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=22 {
              if k<=21 {
                assert k==21;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==22;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=23 {
                assert k==23;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==24;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        } else {
          if k<=28 {
            if k<=26 {
              if k<=25 {
                assert k==25;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==26;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=27 {
                assert k==27;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==28;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=30 {
              if k<=29 {
                assert k==29;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==30;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=31 {
                assert k==31;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==32;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        }
      }
    } else {
      if k<=48 {
        if k<=40 {
          if k<=36 {
            if k<=34 {
              if k<=33 {
                assert k==33;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==34;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=35 {
                assert k==35;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==36;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=38 {
              if k<=37 {
                assert k==37;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==38;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=39 {
                assert k==39;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==40;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        } else {
          if k<=44 {
            if k<=42 {
              if k<=41 {
                assert k==41;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==42;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=43 {
                assert k==43;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==44;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=46 {
              if k<=45 {
                assert k==45;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==46;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=47 {
                assert k==47;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==48;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        }
      } else {
        if k<=56 {
          if k<=52 {
            if k<=50 {
              if k<=49 {
                assert k==49;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==50;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=51 {
                assert k==51;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==52;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=54 {
              if k<=53 {
                assert k==53;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==54;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=55 {
                assert k==55;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==56;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        } else {
          if k<=60 {
            if k<=58 {
              if k<=57 {
                assert k==57;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==58;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=59 {
                assert k==59;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==60;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          } else {
            if k<=62 {
              if k<=61 {
                assert k==61;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==62;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            } else {
              if k<=63 {
                assert k==63;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              } else {
                assert k==64;
                assert x.RotateLeft(k as nat)==(x << k)+(x >> (64-k));
              }
            }
          }
        }
      }
    }
  }

  // Each leaf is an ordinary fixed-count native proof, not a trusted fact.
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv64,k:bv7)
    requires k<=64
    ensures x.RotateRight(k as nat)==(x >> k)+(x << (64-k))
  {
    EmbedCount(k);
    if k<=32 {
      if k<=16 {
        if k<=8 {
          if k<=4 {
            if k<=2 {
              if k<=1 {
                if k<=0 {
                  assert k==0;
                  assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
                } else {
                  assert k==1;
                  assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
                }
              } else {
                assert k==2;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=3 {
                assert k==3;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==4;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=6 {
              if k<=5 {
                assert k==5;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==6;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=7 {
                assert k==7;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==8;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        } else {
          if k<=12 {
            if k<=10 {
              if k<=9 {
                assert k==9;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==10;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=11 {
                assert k==11;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==12;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=14 {
              if k<=13 {
                assert k==13;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==14;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=15 {
                assert k==15;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==16;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        }
      } else {
        if k<=24 {
          if k<=20 {
            if k<=18 {
              if k<=17 {
                assert k==17;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==18;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=19 {
                assert k==19;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==20;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=22 {
              if k<=21 {
                assert k==21;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==22;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=23 {
                assert k==23;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==24;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        } else {
          if k<=28 {
            if k<=26 {
              if k<=25 {
                assert k==25;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==26;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=27 {
                assert k==27;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==28;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=30 {
              if k<=29 {
                assert k==29;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==30;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=31 {
                assert k==31;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==32;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        }
      }
    } else {
      if k<=48 {
        if k<=40 {
          if k<=36 {
            if k<=34 {
              if k<=33 {
                assert k==33;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==34;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=35 {
                assert k==35;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==36;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=38 {
              if k<=37 {
                assert k==37;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==38;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=39 {
                assert k==39;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==40;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        } else {
          if k<=44 {
            if k<=42 {
              if k<=41 {
                assert k==41;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==42;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=43 {
                assert k==43;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==44;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=46 {
              if k<=45 {
                assert k==45;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==46;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=47 {
                assert k==47;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==48;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        }
      } else {
        if k<=56 {
          if k<=52 {
            if k<=50 {
              if k<=49 {
                assert k==49;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==50;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=51 {
                assert k==51;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==52;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=54 {
              if k<=53 {
                assert k==53;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==54;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=55 {
                assert k==55;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==56;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        } else {
          if k<=60 {
            if k<=58 {
              if k<=57 {
                assert k==57;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==58;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=59 {
                assert k==59;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==60;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          } else {
            if k<=62 {
              if k<=61 {
                assert k==61;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==62;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            } else {
              if k<=63 {
                assert k==63;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              } else {
                assert k==64;
                assert x.RotateRight(k as nat)==(x >> k)+(x << (64-k));
              }
            }
          }
        }
      }
    }
  }

  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv64,k:bv7,z:bv64)
    requires k<=64
    requires z==x.RotateLeft(k as nat)
    ensures z as int==R.rolNat(64,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.LeftMeaning(64,x as int,k as nat);
    ComplementCount(k); NativeLeft(x,k);
    var l,r:=x << k,x >> (64-k);
    assert z==l+r;
    S.LeftWitness(x,k,l); S.RightWitness(x,64-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,18446744073709551616);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rolNat(64,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} LeftRotate(x:bv64,k:nat)
    requires k<=64
    ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(64,x as int,k)
  {
    C.RoundTrip7(k); S.CountBound(k as bv7);
    assert (k as bv7)<=64;
    assert x.RotateLeft(k)==x.RotateLeft((k as bv7) as nat);
    LeftWitness(x,k as bv7,x.RotateLeft((k as bv7) as nat));
    F.LeftRotate(64,x as int,k);
  }

  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv64,k:bv7,z:bv64)
    requires k<=64
    requires z==x.RotateRight(k as nat)
    ensures z as int==R.rorNat(64,x as int,k as int)
  {
    S.CountBound(k); B.Power(); A.RightMeaning(64,x as int,k as nat);
    ComplementCount(k); NativeRight(x,k);
    var l,r:=x >> k,x << (64-k);
    assert z==l+r;
    S.RightWitness(x,k,l); S.LeftWitness(x,64-k,r);
    S.PlusWitness(l,r,z);
    A.Finish(z as int,l as int,r as int,18446744073709551616);
    assert z as int==l as int+r as int;
    assert l as int+r as int==R.rorNat(64,x as int,k as int);
  }

  lemma {:induction false} {:isolate_assertions} RightRotate(x:bv64,k:nat)
    requires k<=64
    ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(64,x as int,k)
  {
    C.RoundTrip7(k); S.CountBound(k as bv7);
    assert (k as bv7)<=64;
    assert x.RotateRight(k)==x.RotateRight((k as bv7) as nat);
    RightWitness(x,k as bv7,x.RotateRight((k as bv7) as nat));
    F.RightRotate(64,x as int,k);
  }

}
