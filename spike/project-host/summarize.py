"""Summarize player evidence without putting private case names in aggregate tables."""
import argparse
import collections
import json
import pathlib
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("evidence", type=pathlib.Path)
parser.add_argument("--run", default="all/run-1.json")
parser.add_argument("--label", default="aggregate")
args = parser.parse_args()
root = args.evidence
inventory = json.loads((root / "inventory.json").read_text(encoding="utf-8-sig"))
sample = json.loads((root / "sample.json").read_text(encoding="utf-8-sig"))
discovery = ET.parse(root / "discovery/run-1.json.discovery.xml")
available = {t.get("fullname"): t for t in discovery.iter("test-case")}
report = json.loads((root / args.run).read_text(encoding="utf-8-sig"))
results = {c["name"]: c for c in report["tests"]}
samples = {c["name"] for c in sample}
rows = collections.defaultdict(collections.Counter)
missing = []
failures = []
for case in inventory:
    name = case["name"]
    row = rows[case["bucket"]]
    row["cohort"] += 1
    row["sample"] += name in samples
    row["discovered"] += name in available
    result = results.get(name)
    if result:
        row["completed"] += 1
        outcome = result["outcome"]
        row[outcome] += 1
        row["parity"] += outcome == case["oracle"]
        row["oracleMatched"] += case["oracle"] is not None
        if outcome != case["oracle"]:
            failures.append(dict(case=case, result=result))
    else:
        missing.append(dict(case=case, discovered=name in available))
totals = collections.Counter()
for row in rows.values():
    totals.update(row)
summary = dict(rows={b: dict(rows[b]) for b in sorted(rows)}, totals=dict(totals),
               fatal=report.get("fatal"), unmatchedInputOracle=sum(c["oracle"] is None for c in inventory),
               outcomeNames=list(set(r["outcome"] for r in results.values())))
(root / (args.label + ".json")).write_text(json.dumps(summary, indent=2), encoding="utf-8")
(root / (args.label + "-missing-cases.json")).write_text(json.dumps(missing, indent=2), encoding="utf-8")
(root / (args.label + "-parity-mismatches.json")).write_text(json.dumps(failures, indent=2), encoding="utf-8")
columns = ["cohort", "sample", "discovered", "completed", "Passed", "Failed", "Skipped", "parity"]
lines = ["| Engine bucket | " + " | ".join(columns) + " |",
         "| --- | " + " | ".join("---:" for _ in columns) + " |"]
for bucket, row in sorted(rows.items()):
    lines.append("| " + bucket + " | " + " | ".join(str(row[c]) for c in columns) + " |")
lines.append("| Total | " + " | ".join(str(totals[c]) for c in columns) + " |")
(root / (args.label + "-table.md")).write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps(summary, indent=2))
