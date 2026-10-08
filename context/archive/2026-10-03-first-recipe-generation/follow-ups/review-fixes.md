# Review fixes: first-recipe-generation

Follow-ups queued from `reviews/impl-review.md` triage.

## F1 — Open sign-up with no per-user limit on paid AI calls

- **Decision**: Fix A — deferred as documented risk.
- **Done**: `context/changes/deployment/deployment-plan.md` Phase 8 now marks the Anthropic spend-limit check as a go-live blocker for recipe generation; Phase 10 backlog has a per-user generation throttle item.
- **Open**: implement the per-user throttle before public launch (one generation in flight per user plus a cooldown; must hold across instances if App Service scales out).
