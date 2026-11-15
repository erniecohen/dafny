"""Independent executable semantics for issue 193 (standard library only).

The sequence/arithmetic side uses quotient/remainder and Boolean digits, not
Python's bitwise operations. The native side uses Python bitwise operations and
explicit fixed-width masks. Neither is a model of Z3's implementation.
"""


def p(n):
    if n < 0:
        raise ValueError("the exponent of P is a natural number")
    return 2 ** n


def bits(n, a):
    out = []
    for _ in range(n):
        a, r = divmod(a, 2)
        out.append(r == 1)
    return tuple(out)


def value(s):
    out = 0
    for b in reversed(s):
        out = 2 * out + int(b)
    return out


def and_nat(n, a, b):
    # Iterative evaluation of the exact terminating recurrence in section 2.
    out, weight = 0, 1
    for _ in range(n):
        a, x = divmod(a, 2)
        b, y = divmod(b, 2)
        out += weight * int(x == 1 and y == 1)
        weight *= 2
    return out


def not_nat(n, a):
    m = p(n)
    return m - 1 - a % m


def or_nat(n, a, b):
    return not_nat(n, and_nat(n, not_nat(n, a), not_nat(n, b)))


def xor_nat(n, a, b):
    return and_nat(n, or_nat(n, a, b), not_nat(n, and_nat(n, a, b)))


def and_not_nat(n, a, b):
    return and_nat(n, a, not_nat(n, b))


def shl_nat(n, a, k):
    return 0 if k < 0 or n <= k else ((a % p(n)) * p(k)) % p(n)


def shr_nat(n, a, k):
    return 0 if k < 0 or n <= k else (a % p(n)) // p(k)


def rol_nat(n, a, k):
    if n == 0:
        return 0
    r, m = k % n, p(n)
    u = a % m
    return (u * p(r)) % m + u // p(n - r)


def ror_nat(n, a, k):
    if n == 0:
        return 0
    r, u = k % n, a % p(n)
    return u // p(r) + (u % p(r)) * p(n - r)


def pop_nat(n, a):
    out = 0
    for _ in range(n):
        a, r = divmod(a, 2)
        out += r
    return out


def bool_op(op, s, t):
    if op == "A":
        return tuple(a and b for a, b in zip(s, t))
    if op == "OR":
        return tuple(a or b for a, b in zip(s, t))
    if op == "XOR":
        return tuple(a != b for a, b in zip(s, t))
    if op == "ANDNOT":
        return tuple(a and not b for a, b in zip(s, t))
    raise ValueError(op)


def add_bits(s, t, carry=False):
    """Ripple-carry adder, with the final carry discarded."""
    out = []
    for a, b in zip(s, t):
        out.append((a != b) != carry)
        carry = (a and b) or (carry and (a != b))
    return tuple(out)


def sub_bits(s, t):
    return add_bits(s, tuple(not b for b in t), True)


def mul_digits(s, t):
    """Shift-and-add multiplication, evaluated as a Horner digit fold."""
    m, a, out = p(len(s)), value(s), 0
    for b in reversed(t):
        out = (2 * out + (a if b else 0)) % m
    return out


def div_digits(s, t):
    """Unsigned restoring long division; explicit SMT zero-divisor meaning."""
    b = value(t)
    if b == 0:
        return p(len(s)) - 1, value(s)
    q, r = 0, 0
    for a in reversed(s):
        r, q = 2 * r + int(a), 2 * q
        if r >= b:
            r -= b
            q += 1
    return q, r


def le_bits(s, t):
    for a, b in zip(reversed(s), reversed(t)):
        if a != b:
            return not a
    return True


def shift_bits(s, k, left):
    n = len(s)
    if k < 0 or k >= n:
        return (False,) * n
    if left:
        return (False,) * k + s[:n-k]
    return s[k:] + (False,) * k


def rotate_bits(s, k, left):
    n = len(s)
    if not n:
        return ()
    return tuple(s[(i-k if left else i+k) % n] for i in range(n))


def native(n, op, x, y=0, k=0):
    """Positive-width SMT-style unsigned word operators, independently coded."""
    if n <= 0:
        raise ValueError("SMT fixed-size bitvectors require positive width")
    m = 1 << n
    mask = m - 1
    x, y, c = x & mask, y & mask, k & mask
    if op == "A": return x & y
    if op == "C": return (~x) & mask
    if op == "+": return (x + y) & mask
    if op == "-": return (x - y) & mask
    if op == "*": return (x * y) & mask
    if op == "/": return mask if y == 0 else x // y
    if op == "%": return x if y == 0 else x % y
    if op == "<=": return x <= y
    if op == "OR": return x | y
    if op == "XOR": return x ^ y
    if op == "ANDNOT": return x & ((~y) & mask)
    if op == "L": return 0 if c >= n else (x << c) & mask
    if op == "R": return 0 if c >= n else x >> c
    if op == "ROL":
        r = c % n
        return ((x << r) | (x >> (n-r))) & mask
    if op == "ROR":
        r = c % n
        return ((x >> r) | (x << (n-r))) & mask
    raise ValueError(op)


def checked_cast(n, a):
    if not 0 <= a < p(n):
        raise ValueError("source cast does not fit")
    return a
