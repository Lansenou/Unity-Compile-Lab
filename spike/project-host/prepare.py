"""Prepare a deterministic stratified sample. All case-level output is private."""
import argparse
import collections
import hashlib
import json
import pathlib
import random
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("ucl_output", type=pathlib.Path)
parser.add_argument("oracle", type=pathlib.Path)
parser.add_argument("evidence", type=pathlib.Path)
parser.add_argument("--cohort-size", type=int)
parser.add_argument("--sample-size", type=int, default=200)
parser.add_argument("--seed", type=int, default=1003)
args = parser.parse_args()
args.evidence.mkdir(parents=True, exist_ok=True)
oracle = {t.get("fullname"): t.get("result") for t in ET.parse(args.oracle).iter("test-case")}


def bucket(reason):
    members = [
        ("Internal_CreateGameObject", "GameObject create"),
        ("get_dataPath", "Application.dataPath"),
        ("FindObjectsByType", "FindObjectsByType"),
        ("DebugLogHandler", "Debug.Log"),
        ("Debug.", "Debug.Log"),
        ("CreatePrimitive", "CreatePrimitive"),
        ("activeColorSpace", "activeColorSpace"),
        ("JsonUtility", "JsonUtility"),
        ("SystemInfo", "SystemInfo"),
        ("LogAssert", "LogAssert"),
    ]
    for needle, label in members:
        if needle in reason:
            return label
    if "engine member: " in reason:
        member = reason.split("engine member: ", 1)[1]
        return member.split(".")[1] if member.startswith("UnityEngine.") else "UnityEditor"
    if "constructs UnityEngine." in reason:
        return reason.split("constructs UnityEngine.", 1)[1].split(",", 1)[0].rsplit(".", 1)[-1]
    return "unknown"


cases = []
for line in args.ucl_output.read_text(encoding="utf-8-sig").splitlines():
    if line.startswith("  needs-unity "):
        name, reason = line[len("  needs-unity "):].split(": ", 1)
        cases.append(dict(name=name, reason=reason, bucket=bucket(reason), oracle=oracle.get(name)))
full_count = len(cases)
if args.cohort_size is not None:
    if not 0 < args.cohort_size <= len(cases):
        parser.error("cohort size exceeds input")
    cases = cases[:args.cohort_size]
if len({c["name"] for c in cases}) != len(cases):
    parser.error("duplicate case names in cohort")
groups = collections.defaultdict(list)
for case in cases:
    groups[case["bucket"]].append(case)
randomizer = random.Random(args.seed)
for group in groups.values():
    randomizer.shuffle(group)
size = min(args.sample_size, len(cases))
if size < len(groups):
    parser.error("sample too small to include every bucket")
allocation = {b: 1 for b in groups}
while sum(allocation.values()) < size:
    b = max((b for b in groups if allocation[b] < len(groups[b])),
            key=lambda b: len(groups[b]) / (allocation[b] + 1))
    allocation[b] += 1
sample = [case for b in sorted(groups) for case in groups[b][:allocation[b]]]
for label, selection in [("inventory", cases), ("sample", sample)]:
    (args.evidence / (label + ".json")).write_text(json.dumps(selection, indent=2), encoding="utf-8")
    (args.evidence / ("all-cases.txt" if label == "inventory" else "sample-cases.txt")).write_text(
        "\n".join(c["name"] for c in selection) + "\n", encoding="utf-8")
summary = dict(fullInputCount=full_count, cohortCount=len(cases), sampleCount=len(sample),
               oracleMatched=sum(c["oracle"] is not None for c in cases),
               buckets=dict(collections.Counter(c["bucket"] for c in cases)),
               allocation=allocation,
               inputHashes={p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                            for p in [args.ucl_output, args.oracle]})
(args.evidence / "preparation.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
print(json.dumps(summary, indent=2))
