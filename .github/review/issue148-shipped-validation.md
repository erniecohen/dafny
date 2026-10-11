# Generic Python datatype discriminator regression

The first strict shipped comparison at a2e0e6c0 (public run37230336609)
passes the build, upstream harness, both resolver checks, all eight verifier
shards, all four standard-library comparisons and additional-axiom resource
report. Each verdict comparison differs only by the added registered test:

`git-issues/git-issue-148.dfy`: exit0, 2 verified, 0 errors, no error lines.

There are1081 unchanged old rows and one new row in each option mode. This
expectation commit records the observed intentional inventory addition only.
The program uses an explicit None value and a universal generic ensures clause;
the repaired Python discriminator is a property and receives no default-value
argument. Product code is the tested development change from PR151, ported as
e28ac31a3195a43ed62c0644ed9ba372dbc304b6. No verifier premise or axiom changes.
