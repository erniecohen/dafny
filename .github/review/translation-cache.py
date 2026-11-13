"""Check an actual CLI batch's translation hits and nonempty verification inventory."""
import collections
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    events = [json.loads(line) for line in stream]
translations = collections.defaultdict(list)
for event in events:
    if event.get("ev") == "span" and event.get("stage") == "translate":
        translations[event["module"]].append(event["cached"])
assert translations and {"A", "B"}.issubset(translations), translations
assert all(hits == [False, True] for hits in translations.values()), translations
completed = [event for event in events if event.get("ev") == "task" and "completed" in event]
assert completed, "a cache hit must not discard the current symbols' proof tasks"
print("translation cache: each module misses once then hits; all tasks still run")
