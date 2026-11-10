# Requirements traceability — define, implement, enforce

"Enforced" means something fails automatically when the rule is broken: a test, a database
constraint, a type that refuses the invalid value, or a structural check. Test names are given as
`Class.Method` (UnitTests unless prefixed `Arch:`).

**Status key:** ✅ Implemented · 🟡 Partially implemented (gap stated) · ⛔ Deliberately not implemented

| Rule | How it is defined | Where it is implemented | How it is enforced | Status |
|---|---|---|---|---|
| **R1** Catalogue item: code, description, unit, rate, labour component | `CatalogueItem` + `CatalogueRate`; invariant `0 ≤ labour ≤ rate` | `Domain/Catalogue/CatalogueItem.cs` | Constructor throws (`CatalogueTests.Labour_component_cannot_exceed_the_rate`); DB check constraint `CK_CatalogueRates_Labour`; unique `Code` index | ✅ |
| **R2** Effective-dated rates; line uses rate on the estimate's pricing date | Latest `EffectiveFrom ≤ PricingDate`; no rate → 422. Line snapshots rate, labour, code, description, unit | `CatalogueItem.RateEffectiveOn`, `Estimate.AddOrAdjustLine`, `CatalogueQueries` | `CatalogueTests.Rate_is_the_one_in_force_on_the_pricing_date` (4 boundary cases), `EstimateLinesTests.New_line_is_priced_at_the_estimates_pricing_date_not_today`; unique `(CatalogueItemId, EffectiveFrom)` | ✅ |
| **R3** Quantity ≤ 3 dp; markup ≤ 2 dp, 0–100 | Value objects `Quantity`, `MarkupPercentage`; extra precision is **rejected**, not rounded | `Domain/Pricing/*.cs`; UI mirrors in `shared/validators.ts` | Types refuse invalid values (`PricingTests.*`); columns `decimal(18,3)` / `decimal(5,2)` (`Arch: MigrationTests.Schema_contains_rule_enforcing_constraints`) | ✅ |
| **R4** Net = qty × rate; total = net + markup; money to 2 dp | Markup amount = net × % / 100. Each step rounded half-away-from-zero (DESIGN A4) | `Domain/Pricing/LinePricing.cs`, `Money` | `PricingTests.Line_amounts_follow_R4_with_commercial_rounding` (453.125 → 453.13), `Net_is_rounded_to_money_precision_before_markup_is_applied`; money columns `decimal(18,2)` | ✅ |
| **R5** Estimate total = sum of lines | Sum of stored, rounded line totals, computed by the database | `EstimateQueries.GetTotalsAsync` (covering index) | 🟡 Not covered by an automated test: the sum runs in SQL Server and needs an integration test (see gaps) | 🟡 |
| **R6** Quantity > 0 | `Quantity.Create` | `Domain/Pricing/Quantity.cs` | `PricingTests.Quantity_must_be_greater_than_zero`; `EstimateServiceTests.Invalid_quantity_is_rejected_before_anything_is_saved` | ✅ |
| **R7** Item at most once per estimate; re-adding adjusts the line | Upsert: posted quantity/markup **replace** the existing line (DESIGN A5); 201 created / 200 adjusted | `Estimate.AddOrAdjustLine`, `EstimateService.AddOrAdjustLineAsync` | Unique index `(EstimateId, CatalogueItemId)` (`Arch: MigrationTests`); `EstimateLinesTests.Adding_the_same_catalogue_item_again_adjusts_the_existing_line…` | ✅ |
| **R8** Draft→Submitted→Approved; Submitted→Rejected→Draft; nothing else | State machine in `Estimate` methods; invalid → `InvalidStateTransitionException` (409) | `Domain/Estimates/Estimate.cs` | `EstimateLifecycleTests.Only_the_transitions_in_R8_are_allowed` (10 invalid transitions), `Rejected_estimate_can_return_to_draft_and_be_resubmitted`; `Arch: Domain_types_expose_no_public_setters` (status can't be assigned around the state machine) | ✅ |
| **R9** Submit needs ≥1 line and pricing date ≤ today | Today = UTC date from injected `TimeProvider` (DESIGN A7) | `Estimate.Submit`, `EstimateService.SubmitAsync` | `EstimateLifecycleTests.Submission_requires_at_least_one_line`, `…pricing_date_that_is_not_in_the_future`, `Pricing_date_of_today_can_be_submitted` | ✅ |
| **R10** Approved estimate cannot change | Commercial content (lines, money, pricing date, status) locked outside Draft (DESIGN A1) | `Estimate.EnsureLinesEditable` | `EstimateLinesTests.Lines_cannot_be_added_changed_or_removed_outside_Draft` (Submitted/Approved/Rejected); invalid transitions out of Approved; rowversion stops an edit racing approval | ✅ |
| **R11** PM can correct an approved estimate's description at any time | Added `ProjectManager` role (deviation from R14, DESIGN A1). Any status; audited with old → new text; no status/money change | `Estimate.CorrectDescription`, `EstimateService.CorrectDescriptionAsync`, `PUT /estimates/{id}/description` | `EstimateLifecycleTests.Description_of_an_approved_estimate_can_be_corrected…`; `EstimateServiceTests.Only_project_managers_can_correct_descriptions`; endpoint policy | ✅ |
| **R12** Approval safe to retry: no second approval, no second audit entry | State-idempotent: approving Approved returns 200 and writes nothing; a concurrent loser re-reads and succeeds if it's now Approved | `Estimate.Approve` → `ApprovalOutcome`, `EstimateService.ApproveAsync` | `EstimateLifecycleTests.Approval_is_idempotent_and_writes_a_single_audit_entry`; `EstimateServiceTests.Repeated_approval_returns_success_without_saving_again`, `Approval_that_loses_a_concurrency_race…`, `Approval_conflict_that_did_not_end_in_approval_is_surfaced`; **filtered unique index** `UX_EstimateHistory_SingleApproval` (`Arch: MigrationTests`) | ✅ |
| **R13** Every transition recorded with who and when | `EstimateHistoryEntry` written by the aggregate on every transition (+ creation and description corrections) | `Estimate.Transition/Record` | `EstimateLifecycleTests.Happy_path_records_every_transition_with_who_and_when`. History FK is `Restrict` (no cascade delete of audit) | ✅ |
| **R14** Roles Estimator, Reviewer, Viewer | Enum `UserRole` (+ `ProjectManager`, see R11); role carried in the JWT | `Domain/Users/User.cs`, `JwtTokenIssuer` | Unknown role refused by `User` ctor; tokens without a valid role are rejected by `HttpCurrentUser` | ✅ |
| **R15** Estimators edit/submit drafts; Reviewers approve/reject; Viewers read only | One permissions table; endpoint role policies **and** service checks (defence in depth) | `Application/Security/Permissions.cs`, `AuthConstants.PolicyRoles`, `EstimateService` | `EstimateServiceTests.Only_estimators_can_add_lines` (3 roles), `Only_reviewers_can_approve` (3 roles) | ✅ |
| **R16** Reviewer may not approve an estimate they created | Checked against `CreatedById`, not current role (DESIGN A2). Rejecting own estimate allowed | `Estimate.Approve` | `EstimateLifecycleTests.A_reviewer_cannot_approve_an_estimate_they_created`; `EstimateServiceTests.Reviewer_who_created_the_estimate_cannot_approve_it…` (also checks UI capability) | ✅ |
| **R17** Estimators must not see labour cost | Labour fields omitted from JSON (not null) for roles without `CanViewLabourCost`; line labour columns not even selected | `Permissions.CanViewLabourCost`, `EstimateQueries.Project`, DTO `JsonIgnore(WhenWritingNull)` | `EstimateServiceTests.Estimators_never_receive_labour_cost` (asserts serialized JSON), `Reviewers_and_project_managers_receive_labour_cost` | ✅ |
| **R18** Users only see assigned projects | `ProjectAssignment`; unassigned → **404** for estimates (no existence leak) | `AccessGuard`, `ProjectQueries` | `EstimateServiceTests.Users_not_assigned_to_the_project_get_not_found_not_forbidden` | ✅ |
| **R19** Up to 50 000 lines; API returns an estimate and its lines | Header + DB totals in one call; lines server-paged (≤500/page) and projected without tracking; line edits touch one row | `EstimateQueries`, `GET /estimates/{id}/lines`, covering index, identity PK | `EstimateServiceTests.Page_size_is_bounded`; optional 50 000-line seed (`Seed:LargeEstimateLines`). 🟡 No automated performance test; measured manually only on the in-memory provider (≈0.26 s / 500-line page, ~156 KB) | 🟡 |

## Structural enforcement (not unit tests)

| Mechanism | What breaks it | Where |
|---|---|---|
| Assembly-reference rules | Domain referencing EF Core / ASP.NET / Microsoft.Extensions / other layers; Application referencing EF Core / ASP.NET / SqlClient / Infrastructure; Infrastructure referencing ASP.NET / API | `Arch: LayerDependencyTests` |
| Project-file rules | Any `PackageReference`/`FrameworkReference`/`ProjectReference` in Domain (even unused ones, which the compiler would drop from metadata); Application referencing anything but Domain | `Arch: ProjectFileTests` |
| No public setters in Domain | Adding `{ get; set; }` to an entity (state machine bypass) | `Arch: LayerDependencyTests.Domain_types_expose_no_public_setters` |
| Schema/migration drift | Changing the EF model without a migration | `Arch: MigrationTests.Model_has_no_changes_missing_from_migrations` |
| Database constraints | Duplicate line per item, second approval record, duplicate rate date, labour > rate, duplicate estimate number, wrong precision | `Persistence/Configurations`, migration `InitialCreate` |
| `TreatWarningsAsErrors` | Nullable-reference and other compiler warnings; **NuGet audit** (NU1902/NU1903) fails the build on any package with a known moderate/high vulnerability | `backend/Directory.Build.props`; security pins in Infrastructure/API `.csproj` |

Verified by deliberately adding an EF Core reference and attribute to the Domain: three architecture
tests failed; reverting restored green.

## Known gaps in enforcement

- **No integration tests against real SQL Server.** The unique, filtered and check constraints and the
  rowversion behaviour are verified as *present in the schema* (`MigrationTests`) but not exercised by
  an automated test that hits SQL Server. Next step: Testcontainers (DESIGN §5).
- **No CI pipeline.** Tests run locally with `dotnet test`.
- **In-memory demo mode** (`Database:Provider=InMemory`) does not enforce database constraints or
  concurrency tokens; domain and application rules still apply. It exists for quick evaluation only.
