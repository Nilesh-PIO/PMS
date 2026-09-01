# Module 08 — Authentication & Authorization

Source: `BRD/Doc_BRD.md` has no standalone authentication/authorization section — this module consolidates what the BRD states across **Users and Stakeholders**, **Non-Functional Requirements → Security**, and **Out of Scope**. It doesn't add anything beyond what those sections already say.

## Access model

- **Single user:** the General Physician is the sole primary user (Users and Stakeholders).
- **No secondary users in Phase 1:** "None (Receptionist access not included in Phase 1)."
- Confirmed again under Out of Scope: **"Receptionist or multi-user access"** is explicitly excluded from the initial release.

There is therefore no role/permission model in Phase 1 — a single authenticated user has access to everything the application does. Authorization, in the sense of differentiated access levels, is out of scope by the BRD's own terms; only authentication (proving the one user is who they say they are) applies.

## Authentication requirement

Non-Functional Requirements → Security states:

- **Secure login (single user authentication).**
- **Data encryption (at rest and in transit).**

That is the BRD's complete statement on this topic — it does not specify a login mechanism, session/token model, password policy, session timeout, lockout behavior, or a credential-recovery path for the single user.

## Related modules

- [00-overview.md](00-overview.md) — Users and Stakeholders, Out of Scope.
- [07-non-functional-requirements.md](07-non-functional-requirements.md) — Security is one of six NFR categories; this module is the elaboration of that one category.

## Notes

The BRD's silence here is significant, not incidental: with exactly one user, there is no one else to reset a lost password. `doc/brainstorm-pms-verification.md` treats this as one of its higher-severity findings (a lockout with no recovery path can lock the clinic out of all patient records), and `doc/planning-pms-verification.md` records the adopted mechanism (cookie-based ASP.NET Core Identity, reasoned against the shared-clinic-PC edge cases — screen left unlocked between patients, browser autofill/cache on a shared machine) plus flags credential recovery as a feature still blocked on an owner decision. Encryption at rest/in transit is likewise undecided in the BRD until a deployment model (hosted vs. clinic-local server vs. single PC) is chosen — see those two documents for the current status rather than treating this module as settled.
