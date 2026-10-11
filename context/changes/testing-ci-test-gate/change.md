---
change_id: testing-ci-test-gate
title: Testing ci test gate
status: implementing
created: 2026-10-11
updated: 2026-10-11
archived_at: null
---

## Notes

**Scope decision (2026-10-11):** this change is the CI test gate only: a PR/push workflow (build, tests, pending-model-changes check, optional vulnerable-package check) plus the `main` ruleset that requires it. The full CI/CD (`deploy.yml`, Azure OIDC, deploy job, post-deploy smoke) is deferred until after test phases 2-4. The gate workflow should stay reusable (`workflow_call`) so the later deploy job can depend on it.
