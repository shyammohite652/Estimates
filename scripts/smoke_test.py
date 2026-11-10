#!/usr/bin/env python3
"""
End-to-end smoke test of the business rules over HTTP against a running API (seed data required).
Usage:  python scripts/smoke_test.py [base_url]        (default http://localhost:5080)
Standard library only. Exits non-zero on the first failed check.
"""
import json, sys, urllib.request, urllib.error

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/")
passed = 0

def call(method, path, token=None, body=None):
    req = urllib.request.Request(BASE + path, method=method,
                                 data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json", **({"Authorization": f"Bearer {token}"} if token else {})})
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read()
        return e.code, (json.loads(raw) if raw else None)

def check(name, condition, detail=""):
    global passed
    if not condition:
        print(f"FAIL  {name} {detail}"); sys.exit(1)
    passed += 1
    print(f"ok    {name}")

def login(email):
    s, b = call("POST", "/api/auth/login", body={"email": email})
    assert s == 200, (s, b)
    return b["accessToken"]

est1, est2, rev1, rev2, viewer, pm = (login(f"{u}@example.com") for u in
    ["estimator1", "estimator2", "reviewer1", "reviewer2", "viewer1", "manager1"])

# R18 — users only see assigned projects
s, projects = call("GET", "/api/projects", est1)
names = {p["name"] for p in projects}
check("R18 estimator1 sees RES + COM, not INF", names == {"Construction of Residential Building", "Commercial Office Development"}, names)
s, vp = call("GET", "/api/projects", viewer)
check("R18 viewer1 sees only RES", [p["code"] for p in vp] == ["RES-001"])
s, _ = call("GET", "/api/projects", None)
check("Unauthenticated request is 401", s == 401, s)

by_number = {(p["code"], e["number"]): e for p in call("GET", "/api/projects", pm)[1] for e in p["estimates"]}
res_draft = by_number[("RES-001", "EST-0001")]["id"]
res_submitted = by_number[("RES-001", "EST-0002")]["id"]
res_approved = by_number[("RES-001", "EST-0003")]["id"]
com_by_reviewer = by_number[("COM-002", "EST-0001")]["id"]
com_future = by_number[("COM-002", "EST-0002")]["id"]
com_rejected = by_number[("COM-002", "EST-0003")]["id"]
inf_draft = by_number[("INF-003", "EST-0001")]["id"]

s, _ = call("GET", f"/api/estimates/{inf_draft}", est1)
check("R18 unassigned estimate returns 404 (not 403)", s == 404, s)

# R17 — labour cost visibility
s, lines = call("GET", f"/api/estimates/{res_draft}/lines", est1)
check("R17 estimator lines omit labour fields", all("unitLabourCost" not in l and "labourAmount" not in l for l in lines["items"]))
s, head = call("GET", f"/api/estimates/{res_draft}", est1)
check("R17 estimator header omits labourTotal", "labourTotal" not in head)
s, cat = call("GET", "/api/catalogue?pricingDate=2026-03-15", est1)
check("R17 estimator catalogue omits labour component", all("labourComponent" not in c for c in cat))
s, rlines = call("GET", f"/api/estimates/{res_draft}/lines", rev1)
check("R17 reviewer sees labour", all("unitLabourCost" in l for l in rlines["items"]))
s, plines = call("GET", f"/api/estimates/{res_draft}/lines", pm)
check("R17 project manager sees labour", all("labourAmount" in l for l in plines["items"]))

# R2 — rate by pricing date (Mar → Jan rate; Aug → Jul rate)
conc = next(l for l in lines["items"] if l["itemCode"] == "CONC-C30")
check("R2 March pricing uses 1450.00", conc["unitRate"] == 1450.00, conc["unitRate"])
s, sub_lines = call("GET", f"/api/estimates/{res_submitted}/lines", est1)
conc_jul = next(l for l in sub_lines["items"] if l["itemCode"] == "CONC-C30")
check("R2 August pricing uses 1512.50", conc_jul["unitRate"] == 1512.50, conc_jul["unitRate"])

# R4/R5 — arithmetic and totals
check("R4 net = qty × rate", conc["netAmount"] == round(conc["quantity"] * conc["unitRate"], 2), conc)
check("R5 header total = sum of line totals", abs(head["total"] - sum(l["lineTotal"] for l in lines["items"])) < 0.001)

# R3/R6 — invalid inputs
item = conc["catalogueItemId"]
s, b = call("POST", f"/api/estimates/{res_draft}/lines", est1, {"catalogueItemId": item, "quantity": 0, "markupPercent": 5})
check("R6 quantity 0 → 400", s == 400 and "quantity" in b.get("errors", {}), (s, b))
s, b = call("POST", f"/api/estimates/{res_draft}/lines", est1, {"catalogueItemId": item, "quantity": 1.2345, "markupPercent": 5})
check("R3 quantity with 4 dp → 400", s == 400, s)
s, b = call("POST", f"/api/estimates/{res_draft}/lines", est1, {"catalogueItemId": item, "quantity": 1, "markupPercent": 100.5})
check("R3 markup > 100 → 400", s == 400, s)

# R7 — same item adjusts the line
before = head["lineCount"]
s, line = call("POST", f"/api/estimates/{res_draft}/lines", est1, {"catalogueItemId": item, "quantity": 2.5, "markupPercent": 12.5})
s2, head2 = call("GET", f"/api/estimates/{res_draft}", est1)
check("R7 re-adding an item returns 200 (adjusted) and keeps line count", s == 200 and head2["lineCount"] == before, (s, head2["lineCount"]))
check("R4 commercial rounding 453.125 → 453.13", line["markupAmount"] == 453.13 and line["lineTotal"] == 4078.13, line)

# R15 — role permissions
s, _ = call("POST", f"/api/estimates/{res_draft}/lines", viewer, {"catalogueItemId": item, "quantity": 1, "markupPercent": 0})
check("R15 viewer cannot edit (403)", s == 403, s)
s, _ = call("POST", f"/api/estimates/{res_submitted}/approve", est1)
check("R15 estimator cannot approve (403)", s == 403, s)

# R9 — submission rules
s, b = call("POST", f"/api/estimates/{com_future}/submit", est2)
check("R9 future pricing date → 422", s == 422 and b["code"] == "pricing_date_in_future", (s, b))
s, created = call("POST", f"/api/projects/{projects[0]['id']}/estimates", est1, {"description": "Empty one", "pricingDate": "2026-11-01"})
check("Estimator can create a draft (201)", s == 201, s)
s, b = call("POST", f"/api/estimates/{created['id']}/submit", est1)
check("R9 no lines → 422", s == 422 and b["code"] == "no_lines", (s, b))

# R16 — reviewer cannot approve own estimate
s, d = call("GET", f"/api/estimates/{com_by_reviewer}", rev1)
check("R16 capabilities say canApprove=false for creator", d["capabilities"]["canApprove"] is False)
s, b = call("POST", f"/api/estimates/{com_by_reviewer}/approve", rev1)
check("R16 self-approval → 403", s == 403 and b["code"] == "self_approval_not_allowed", (s, b))

# R12/R13 — approve twice
s1, a1 = call("POST", f"/api/estimates/{res_submitted}/approve", rev1)
s2, a2 = call("POST", f"/api/estimates/{res_submitted}/approve", rev1)
s, hist = call("GET", f"/api/estimates/{res_submitted}/history", rev1)
check("R12 approve twice → 200, 200", (s1, s2) == (200, 200) and a2["status"] == "Approved", (s1, s2))
check("R12/R13 exactly one approval audit entry with user", [h["userName"] for h in hist if h["action"] == "Approved"] == ["Chitra Reviewer"])

# R10 — approved is immutable; R11 PM correction allowed
s, al = call("GET", f"/api/estimates/{res_approved}/lines", est1)
s, b = call("PUT", f"/api/estimates/{res_approved}/lines/{al['items'][0]['id']}", est1, {"quantity": 1, "markupPercent": 0})
check("R10 editing an approved estimate's own line → 409", s == 409 and b["code"] == "estimate_not_editable", (s, b))
s, b = call("PUT", f"/api/estimates/{res_approved}/description", est1, {"description": "hack"})
check("R11 estimator cannot correct description (403)", s == 403, s)
s, b = call("PUT", f"/api/estimates/{res_approved}/description", pm, {"description": "Masonry package (corrected)"})
check("R11 PM corrects approved description, status unchanged", s == 200 and b["status"] == "Approved" and b["description"].endswith("(corrected)"), (s, b))

# R8 — invalid transitions and the reject → reopen path
s, b = call("POST", f"/api/estimates/{res_draft}/approve", rev1)
check("R8 approving a Draft → 409", s == 409 and b["code"] == "invalid_transition", (s, b))
s, b = call("POST", f"/api/estimates/{com_rejected}/reopen", est2)
check("R8 Rejected → Draft by estimator", s == 200 and b["status"] == "Draft", (s, b))

print(f"\nAll {passed} checks passed against {BASE}")
