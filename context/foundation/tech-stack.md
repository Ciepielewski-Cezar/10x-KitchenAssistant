---
starter_id: dotnet-blazor
package_manager: dotnet
project_name: kitchen-assistant
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: first-class
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: false
---

## Why this stack

A solo developer building Kitchen Assistant, a medium-scale web app with a 3-week after-hours MVP budget, chose the .NET language family and accepted the recommended ASP.NET Core default. They then swapped it for the Blazor Web App variant of the same stack, because the API-only webapi template lacks the UI and auth the PRD requires. The official `dotnet new blazor --auth Individual` template provides a C# UI with interactive server rendering, ASP.NET Core Identity for email/password accounts and private per-user spaces (FR-001), and EF Core with migrations on SQL Server, all in one deployable project. SQL Server was chosen over PostgreSQL because the template supports it natively: LocalDB for local development and Azure SQL Database in production need no post-scaffold provider swap or migration regeneration. It clears all four agent-friendly gates. Its scaffolding confidence is first-class rather than verified because this variant has not yet been run end-to-end by the bootstrapper. AI recipe generation (FR-005) still needs a manually added LLM SDK with structured output. Payments, realtime and background jobs are out of scope per the PRD. Deployment targets Azure App Service, with GitHub Actions auto-deploying on merge to main.
