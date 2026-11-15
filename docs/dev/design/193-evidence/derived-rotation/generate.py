#!/usr/bin/env python3
"""Generate exact width-specialized proof adapters from the ten-fact basis."""
from pathlib import Path

def generate(n):
    m = 2 ** n
    ty = f'bv{n}'
    out = ['include "pure-math.dfy"', '', 'import opened BitMath', '',
           '// Only explicit local A, +, L, R instances below are trusted.',
           '// Native decomposition and integer lemmas are proved.', '']
    control = [f'include "derived-{n}.dfy"', '',
               '// Grounded, inhabited controls for the proved adapter summaries.',
               '// Q1 separately checks the complete raw bridge context.', '']
    ablation = [f'include "derived-{n}.dfy"', '',
                '// Diagnostic composed-input clients; retain resource outcomes.', '']
    out += [f'lemma {{:induction false}} p{n}()', f'  ensures p({n}) == {m}',
            '{', '  reveal p();']
    for k in range(n+1):
        out.append(f'  assert p({k}) == {2**k};')
    out += ['}', '']
    out += [f'lemma roundTrip{n}(a: nat)', f'  requires a < {m}',
            f'  ensures ((a as {ty}) as int) == a', '{}', '']
    small_count_bits = (n+1).bit_length()
    out += [f'lemma countBound{n}(k: nat)', f'  requires k <= {n}',
            f'  ensures (k as {ty}) < {2**small_count_bits}', '{}', '']
    for direction in ['Left', 'Right']:
        for k in range(n+1):
            fixed = f'(x << {k}) + (x >> {n-k})' if direction=='Left' else f'(x >> {k}) + (x << {n-k})'
            out += [f'lemma native{direction}{n}_{k}(x: {ty})',
                    f'  ensures x.Rotate{direction}({k}) == {fixed}', '{}', '']
        expr = f'(x << (k as {ty})) + (x >> (({n} - k) as {ty}))' if direction=='Left' else f'(x >> (k as {ty})) + (x << (({n} - k) as {ty}))'
        budget = ' {:resource_limit 2000000}' if n == 64 else ''
        out += [f'lemma {{:induction false}} {{:isolate_assertions}}{budget} native{direction}{n}(x: {ty}, k: nat)',
                f'  requires k <= {n}', f'  ensures x.Rotate{direction}(k) == {expr}', '{']
        def cases(lo, hi, indent):
            if lo == hi:
                j = lo
                out.extend([indent+f'assert k == {j};',
                    indent+f'assert (k as {ty}) == ({j} as {ty});',
                    indent+f'assert (({n} - k) as {ty}) == ({n-j} as {ty});',
                    indent+f'native{direction}{n}_{j}(x);',
                    indent+f'assert x.Rotate{direction}(k) == {expr};'])
            else:
                mid = (lo + hi) // 2
                out.append(indent+f'if k <= {mid} {{')
                cases(lo, mid, indent+'  ')
                out.append(indent+'} else {')
                cases(mid+1, hi, indent+'  ')
                out.append(indent+'}')
        cases(0, n, '  ')
        out += ['}', '']
    out += [f'lemma {{:induction false}} bit{n}(x: {ty}, k: nat)',
            f'  requires k < {n}',
            f'  ensures (((x >> k) & (1 as {ty})) as int) == (x as int) / p(k) % 2',
            '{', '  var a := x as int;', '  var y := x >> k;',
            '  // R: local logical-right-shift bridge.',
            '  assume {:axiom} (y as int) == a / p(k);',
            '  // A: local AND bridge at the observed low-bit operation.',
            f'  assume {{:axiom}} (((y & (1 as {ty})) as int)) == andNat({n}, y as int, 1);',
            f'  lowBit({n}, y as int);', '}', '']
    for direction in ['Left', 'Right']:
        first = f'x << (k as {ty})' if direction=='Left' else f'x >> (k as {ty})'
        second = f'x >> (({n} - k) as {ty})' if direction=='Left' else f'x << (({n} - k) as {ty})'
        vfirst = f'(a * p(k)) % {m}' if direction=='Left' else 'a / p(k)'
        vsecond = f'a / p({n} - k)' if direction=='Left' else f'(a * p({n} - k)) % {m}'
        ref = 'rolNat' if direction=='Left' else 'rorNat'
        # Keep the measured proof order; pure arithmetic first avoids a
        # 4.12.1 resource failure in the 32-bit right-rotation quotient call.
        pure = [f'  rotate{direction}Meaning({n}, a, k);',
                f'  var s := {vfirst} + {vsecond};',
                f'  quotient(s, {m}, 0, s);']
        pure_first = n == 32 and direction == 'Right'
        budget = ' {:resource_limit 2000000}' if n in (16, 32, 64) or (n == 8 and direction == 'Left') else ''
        out += [f'lemma {{:induction false}} {{:isolate_assertions}}{budget} rotate{direction}{n}(x: {ty}, k: nat)',
                f'  requires k <= {n}',
                f'  ensures (x.Rotate{direction}(k) as int) == {ref}({n}, x as int, k)',
                '{', f'  p{n}();', f'  countBound{n}(k);', f'  countBound{n}({n} - k);',
                f'  native{direction}{n}(x, k);',
                f'  var l, r := {first}, {second};',
                f'  assert x.Rotate{direction}(k) == l + r;',
                f'  assert (x.Rotate{direction}(k) as int) == ((l + r) as int);',
                '  var a := x as int;']
        if pure_first:
            out += pure
        out += [
                '  // L/R: each is the corresponding basic local shift instance.',
                f'  assume {{:axiom}} (l as int) == {vfirst};',
                f'  assume {{:axiom}} (r as int) == {vsecond};',
                '  // +: local modular-addition bridge.',
                f'  assume {{:axiom}} ((l + r) as int) == ((l as int) + (r as int)) % {m};']
        if not pure_first:
            out += pure
        out += [
                '  assert ((l + r) as int) == s;',
                f'  assert (x.Rotate{direction}(k) as int) == s;', '}', '']
    specifications = [
        ('bit', f'k < {n}', f'(((x >> k) & (1 as {ty})) as int) == (x as int) / p(k) % 2'),
        ('rotateLeft', f'k <= {n}', f'(x.RotateLeft(k) as int) == rolNat({n}, x as int, k)'),
        ('rotateRight', f'k <= {n}', f'(x.RotateRight(k) as int) == rorNat({n}, x as int, k)')]
    for name, guard, spec in specifications:
        out += [f'lemma client_{name}{n}(x: {ty}, k: nat)', f'  requires {guard}',
                f'  ensures {spec}', f'{{ {name}{n}(x, k); }}', '']
        control += [
                '// This checks the proved adapter summary; base-fact controls are separate.',
                f'lemma control_{name}{n}()', '{', f'  p{n}();', '  pOne();',
                f'  powerSum(1, {n-1});', f'  {name}{n}(5 as {ty}, 1);',
                '  assert false;', '}', '']
        converted = spec.replace('x as int', 'a').replace('x >>', f'(a as {ty}) >>').replace('x.Rotate', f'(a as {ty}).Rotate')
        ablation += [f'lemma {{:induction false}} client_composed_{name}{n}(a: nat, k: nat)',
                f'  requires a < {m} && {guard}', f'  ensures {converted}',
                f'{{ roundTrip{n}(a); {name}{n}(a as {ty}, k); }}', '']
    composed = '\n'.join(ablation).replace(
        '// Diagnostic composed-input clients; retain resource outcomes.',
        '// The same composed-input clients with explicit assertion isolation.').replace(
        '{:induction false}', '{:induction false} {:isolate_assertions}')
    control_composed = '\n'.join(control).replace(
        f'include "derived-{n}.dfy"', f'include "derived-composed-{n}.dfy"')
    for name, _, _ in specifications:
        control_composed = control_composed.replace(
            f'control_{name}{n}', f'control_composed_{name}{n}').replace(
            f'{name}{n}(5 as {ty}, 1)', f'client_composed_{name}{n}(5, 1)')
    return {'derived': '\n'.join(out), 'control_derived': '\n'.join(control),
            'derived-composed': composed, 'control_derived-composed': control_composed,
            'ablation-composed': '\n'.join(ablation)}

if __name__ == '__main__':
    root = Path(__file__).parent
    for n in (8, 16, 32, 64):
        for prefix, source in generate(n).items():
            (root/f'{prefix}-{n}.dfy').write_text(source+'\n')
