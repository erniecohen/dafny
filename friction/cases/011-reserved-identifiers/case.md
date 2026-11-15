# 011: `var` and `assigned` are reserved and cannot name declarations

- **Status:** tip
- **Seen on:** Dafny 4.11.0+fcb2042d.review.bcc0c421 (this fork's release), Z3 5.1.0
- **Options:** none beyond the shared ones (`dfyconfig.toml`)

Workaround accepted, no fix wanted.

## Reproducer

`repro.dfy`:

```dafny
predicate assigned(x: int) { x > 0 }
function var(): int { 0 }
```

## Expected / observed

Both names are reserved words (`var` for local variables, `assigned` for the
definite-assignment expression `assigned(x)`). Each declaration is a parse
error, "invalid NoUSIdentOrDigits"; the `var` one also produces a confusing
second error, "fields are not allowed to be declared at the module level".
A call such as `assigned(3)` elsewhere fails with "invalid Ident".

## Workaround (accepted)

Use another name, for example `var_` or `isAssigned` (`workaround.dfy`).

## Suggested fix

None wanted for the names: this is a tip. The message names a grammar
production, not the cause; "`var` is a reserved word" would save the search.

## Source

Met in a downstream project.

## Check

```check
exit 2
message "repro.dfy(2,10): Error: invalid NoUSIdentOrDigits"
message "repro.dfy(3,9): Error: invalid NoUSIdentOrDigits"
```

## Last recheck

2026-10-30, Dafny 4.11.0+fcb2042d.review.bcc0c421, Z3 5.1.0: TIP-HOLDS, with the figures above. The workaround verifies.
