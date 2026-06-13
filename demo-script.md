# Demo Script — Contract Clause Intelligence Assistant

Five questions for the live demo, in order. Seed first (`POST /api/seed` or the
Seed button) and wait until all three contracts show **Ready**.

The corpus: a mutual NDA (Meridian ↔ Bluewater), a Master Services Agreement
(Meridian ↔ Northgate), and a SaaS subscription agreement (Cloudhaven ↔
Northgate) — overlapping clause types with deliberately different terms.

---

### 1. Simple lookup (warm-up)

> **What notice period is required to terminate the Master Services Agreement for convenience?**

Expect: 60 days' written notice, cited to *MSA — Clause 4* (Termination), high
confidence. Shows clause-level citation with page number.

### 2. Numeric term buried in legalese

> **How long do confidentiality obligations survive after the NDA terminates?**

Expect: five (5) years from disclosure (trade secrets longer), cited to
*Mutual NDA — Clause 5* (Term and Termination).

### 3. Cross-contract comparison

> **Compare the limitation of liability caps across the three contracts.**

Expect: MSA caps at fees paid in the preceding **12 months**, the SaaS agreement
at fees paid in the preceding **6 months**, the NDA at a flat **US $500,000** —
with one citation per contract. This is the hybrid-retrieval showcase: the same
concept is worded differently in each document.

### 4. Operational SLA detail

> **What uptime does the SaaS vendor commit to, and what is the remedy if they miss it?**

Expect: 99.9% monthly uptime; service credits of 5% per 0.1% shortfall capped at
30%, credits as sole remedy — cited to *SaaS Agreement — Clause 2* (Service
Levels and Support).

### 5. The honest "no" (cannot be answered from the corpus)

> **What do the contracts say about employee stock option vesting schedules?**

Expect: **"Not enough context in the contracts"** — no answer is invented. This
demonstrates clause-anchored generation: the model refuses rather than
hallucinating, per the UC07.1 accuracy requirement.

---

**Watch during the demo:** the confidence badge (reranker-score driven), the
latency readout under each answer (target ≤ 3 s), and the citation chips that
expand to the exact clause text and page number.
