// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

//! Semantic-width operations used only by the opt-in integer-newtype lowering.
//! Verified callers supply nonnegative words below 2^width and counts in 0..=width.

use crate::DafnyInt;
use num::{bigint::BigInt, One, Zero};
#[cfg(feature = "small-int")]
use num::ToPrimitive;

fn integer(value: &DafnyInt) -> BigInt {
    #[cfg(not(feature = "small-int"))]
    { crate::dafny_int_to_bigint(value) }
    #[cfg(feature = "small-int")]
    { BigInt::from(*value.as_ref()) }
}

fn result(value: BigInt) -> DafnyInt {
    #[cfg(not(feature = "small-int"))]
    { crate::bigint_to_dafny_int(&value) }
    #[cfg(feature = "small-int")]
    { DafnyInt::new(value.to_i128().expect("integer bitwise result exceeds the small-int runtime range")) }
}

fn mask(width: &DafnyInt) -> BigInt {
    (BigInt::one() << width.as_usize()) - BigInt::one()
}

#[allow(non_snake_case)]
pub fn andNat(_width: &DafnyInt, a: &DafnyInt, b: &DafnyInt) -> DafnyInt {
    result(integer(a) & integer(b))
}
#[allow(non_snake_case)]
pub fn orNat(_width: &DafnyInt, a: &DafnyInt, b: &DafnyInt) -> DafnyInt {
    result(integer(a) | integer(b))
}
#[allow(non_snake_case)]
pub fn xorNat(_width: &DafnyInt, a: &DafnyInt, b: &DafnyInt) -> DafnyInt {
    result(integer(a) ^ integer(b))
}
#[allow(non_snake_case)]
pub fn notNat(width: &DafnyInt, a: &DafnyInt) -> DafnyInt {
    result(!integer(a) & mask(width))
}
#[allow(non_snake_case)]
pub fn shlNat(width: &DafnyInt, a: &DafnyInt, k: &DafnyInt) -> DafnyInt {
    result((integer(a) << k.as_usize()) & mask(width))
}
#[allow(non_snake_case)]
pub fn shrNat(_width: &DafnyInt, a: &DafnyInt, k: &DafnyInt) -> DafnyInt {
    result(integer(a) >> k.as_usize())
}
#[allow(non_snake_case)]
pub fn rolNat(width: &DafnyInt, a: &DafnyInt, k: &DafnyInt) -> DafnyInt {
    let (a, k, n) = (integer(a), k.as_usize(), width.as_usize());
    result(((&a << k) & mask(width)) | (&a >> (n - k)))
}
#[allow(non_snake_case)]
pub fn rorNat(width: &DafnyInt, a: &DafnyInt, k: &DafnyInt) -> DafnyInt {
    let (a, k, n) = (integer(a), k.as_usize(), width.as_usize());
    result((&a >> k) | ((&a << (n - k)) & mask(width)))
}
#[allow(non_snake_case)]
pub fn popNat(_width: &DafnyInt, a: &DafnyInt) -> DafnyInt {
    let mut bits = integer(a);
    let mut count = BigInt::zero();
    while !bits.is_zero() {
        bits &= &bits - BigInt::one();
        count += BigInt::one();
    }
    result(count)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn semantic_width_and_endpoint_counts() {
        let (n, a, k, full, zero) = (DafnyInt::from(12), DafnyInt::from(16),
            DafnyInt::from(8), DafnyInt::from(12), DafnyInt::from(0));
        assert_eq!(notNat(&n, &a), DafnyInt::from(4079));
        assert_eq!(shlNat(&n, &a, &k), zero);
        assert_eq!(shrNat(&n, &a, &k), zero);
        assert_eq!(rolNat(&n, &a, &k), DafnyInt::from(1));
        assert_eq!(rorNat(&n, &a, &k), DafnyInt::from(256));
        for endpoint in [&zero, &full] {
            assert_eq!(rolNat(&n, &a, endpoint), a);
            assert_eq!(rorNat(&n, &a, endpoint), a);
        }
        assert_eq!(shlNat(&n, &a, &full), zero);
        assert_eq!(shrNat(&n, &a, &full), zero);
        assert_eq!(popNat(&n, &DafnyInt::from(255)), DafnyInt::from(8));
        assert_eq!(popNat(&n, &DafnyInt::from(5)), DafnyInt::from(2));
        assert_eq!(popNat(&n, &zero), zero);
    }

    #[test]
    fn boolean_digits_and_full_count() {
        let (n, a, b) = (DafnyInt::from(8), DafnyInt::from(5), DafnyInt::from(3));
        assert_eq!(andNat(&n, &a, &b), DafnyInt::from(1));
        assert_eq!(orNat(&n, &a, &b), DafnyInt::from(7));
        assert_eq!(xorNat(&n, &a, &b), DafnyInt::from(6));
        // A count is never narrowed modulo an underlying carrier's width.
        let (n, k, a) = (DafnyInt::from(80), DafnyInt::from(64), DafnyInt::from(1));
        assert_eq!(shlNat(&n, &a, &k), DafnyInt::from(18446744073709551616u128));
        assert_eq!(shrNat(&n, &a, &k), DafnyInt::from(0));
        assert_eq!(rolNat(&n, &a, &k), DafnyInt::from(18446744073709551616u128));
        assert_eq!(rorNat(&n, &a, &k), DafnyInt::from(65536));
        assert_eq!(popNat(&n, &DafnyInt::from(18446744073709551617u128)), DafnyInt::from(2));
    }

    #[cfg(not(feature = "small-int"))]
    #[test]
    fn arbitrary_precision_words() {
        let n = DafnyInt::from(256);
        let a = result((BigInt::one() << 256) - BigInt::one());
        assert_eq!(popNat(&n, &a), DafnyInt::from(256));
        assert_eq!(notNat(&n, &a), DafnyInt::from(0));
        assert_eq!(rolNat(&n, &a, &DafnyInt::from(129)), a);
        let high = DafnyInt::from(340282366920938463463374607431768211455u128);
        assert_eq!(popNat(&DafnyInt::from(128), &high), DafnyInt::from(128));
        assert_eq!(xorNat(&DafnyInt::from(128), &high, &DafnyInt::from(1)),
            DafnyInt::from(340282366920938463463374607431768211454u128));
    }

    #[cfg(feature = "small-int")]
    #[test]
    #[should_panic(expected = "integer bitwise result exceeds the small-int runtime range")]
    fn small_int_does_not_truncate_results() {
        shlNat(&DafnyInt::from(128), &DafnyInt::from(1), &DafnyInt::from(127));
    }
}
