#!/usr/bin/env python3
"""Change only patterns in genuine Dafny boxed-ISet VCs; formulas are identical."""
import argparse,copy,csv,hashlib,json,re,subprocess
from pathlib import Path
TOKEN=re.compile(r'\s+|;[^\n]*|[()]|"(?:[^"]|"")*"|\|[^|]*\||[^\s()]+')
def parse(s):
 roots=[];stack=[]
 for m in TOKEN.finditer(s):
  t=m[0]
  if t.isspace() or t.startswith(';'):continue
  if t=='(':
   a=[]
   if stack:stack[-1].append(a)
   else:roots.append(a)
   stack.append(a)
  elif t==')':stack.pop()
  else:stack[-1].append(t)
 assert not stack
 return roots
def emit(t):return '('+' '.join(emit(x) if isinstance(x,list) else x for x in t)+')'
