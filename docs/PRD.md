# PRD — Octopus Platform (BIM App Framework)
Version: 0.1
Owner: Ibrahim
Last Updated: 2026-01-25

## 0. Summary
Octopus is a framework for building BIM applications that combines:
1) Octopus.Server: a ready-to-run backend appliance for BIM ingestion, processing, storage, governance (Workspaces/Projects/Users), and CDE-like file management.
2) Octopus.Blazor: a Blazor UI component kit + services that communicate with Octopus.Server via generated API clients.
3) Octopus.Server.Client: API clients generated from OpenAPI as the source of truth.
4) Octopus.Cli + templates: one-command scaffolding for an Aspire-based solution that runs server + web app locally with good defaults.

This PRD is written to support long-running agent development (Ralph loop / harness):
- A structured feature list file (JSON) drives incremental work.
- A progress log persists state across agent sessions.
- Each iteration implements exactly one feature, runs feedback loops, then commits.

## 1. Goals
### 1.1 Product goals
- Provide an out-of-the-box, self-hostable “BIM backend + UI kit” that enables teams to build BIM apps quickly.
- Deliver CDE-like capabilities: file registry, artifacts, lineage, usage/quota tracking, workspace/project governance.
- Make the integration ergonomic: generated clients + Blazor wrapper services + declarative UI components.

### 1.2 Developer experience goals
- `dotnet new octopus` produces a runnable Aspire AppHost solution with:
  - Octopus.Server (API)
  - Octopus.Web (Blazor app consuming Octopus.Blazor + Octopus.Server.Client)
  - Optional infra resources (SQL/Postgres, storage emulator) declared in AppHost
- The server exposes OpenAPI; clients are generated deterministically and packaged.
- A new user can: create workspace → project → upload IFC → process → view model → inspect properties.

## 2. Non-goals (initial releases)
- Full enterprise CDE workflows (transmittals, approvals, ISO 19650) — later.
- Full BCF issue lifecycle — later.
- Multi-queue backends (RabbitMQ/Azure Service Bus) — later; start with in-memory channel.
- Fine-grained per-file ACLs (beyond project roles) — later.

## 3. Personas and primary use cases
### 3.1 Personas
- BIM App Developer: wants reusable UI building blocks + stable server APIs.
- BIM Coordinator/Manager: wants workspaces/projects, user access, files, models, and traceability.
- IT/Platform Engineer: wants pluggable storage/persistence and OIDC integration.

### 3.2 Primary use cases
- UC1: Workspace and Project governance
  - Create workspace, create project, invite users, assign roles.
- UC2: CDE-like file ingestion and storage accounting
  - Upload IFC and other files; track storage usage; enforce quotas (optional).
- UC3: BIM processing pipeline
  - Convert IFC → WexBIM; extract properties; store artifacts; show status.
- UC4: BIM viewing and inspection
  - Web UI loads WexBIM artifact; property queries by selection/filter/paging.
- UC5: Extensibility
  - Add viewer tools/plugins; add server-side processors.

## 4. Architecture (high level)
### 4.1 Monorepo structure (Octopus umbrella repo)
- src/
  - Octopus.Blazor/
  - Octopus.Server.App/
  - Octopus.Server.Domain/
  - Octopus.Server.Contracts/
  - Octopus.Server.Abstractions/
  - Octopus.Server.Persistence.EfCore/
  - Octopus.Server.Storage.LocalDisk/
  - Octopus.Server.Storage.AzureBlob/ (later milestone)
  - Octopus.Server.Processing/
  - Octopus.Server.Client/ (generated)
  - Octopus.Cli/
  - Octopus.AppHost/ (Aspire)
  - Octopus.ServiceDefaults/ (Aspire)
- samples/
  - Octopus.Sample.EndToEnd/
- templates/
  - octopus/
- docs/
  - PRD.md
  - feature_list.json
  - claude-progress.txt

### 4.2 Core domain model
- Workspace
- Project
- User (external subject or local identity)
- WorkspaceMembership (Owner/Admin/Member/Guest)
- ProjectMembership (ProjectAdmin/Editor/Viewer)
- File (first-class registry; uploads and artifacts)
- FileLink (DerivedFrom, ThumbnailOf, PropertiesOf, LogOf, etc.)
- UploadSession (reserve → upload → commit)
- Model
- ModelVersion (references IFC FileId and artifact FileIds)
- ProcessingJob (queue items; status and logs)

### 4.3 Storage and persistence separation
- Blob/object storage (LocalDisk/AzureBlob/S3 later): stores bytes (IFC, WexBIM, properties, logs).
- Metadata persistence (EF Core SQL Server/Postgres; FileBased later): stores entities and relationships.
- Storage usage computed from File registry (sum SizeBytes for non-deleted, latest versions).

### 4.4 API contract and clients
- OpenAPI served from Octopus.Server.App (source of truth).
- Octopus.Server.Client generated at build time (NSwag or equivalent).
- Octopus.Blazor wraps generated clients behind UI services:
  - IWorkspacesService, IProjectsService, IFilesService, IModelsService, IArtifactsService, IUsageService.

### 4.5 Aspire AppHost
- Octopus.AppHost declares:
  - Octopus.Server.App
  - Octopus.Web
  - Optional SQL/Postgres
  - Optional storage emulator
- Local dev uses AppHost as the entrypoint.

## 5. Authentication and authorization (out-of-the-box)
### 5.1 Modes
- Development mode (default for templates): dev auth principal; auto-provision user + personal workspace.
- OIDC/JWT mode (recommended): validate tokens via Authority + Audience; user auto-provision by `sub`.
- (Optional later) Local identity mode: ASP.NET Core Identity for on-prem.

### 5.2 Authorization
- WexServer (Octopus.Server) owns authorization using membership tables.
- IdP provides authentication only; future optional group-to-role mapping.

## 6. Quality bar and feedback loops (non-negotiable)
Each feature must satisfy:
- Build: `dotnet build`
- Tests: `dotnet test`
- Format/lint: `dotnet format` (or configured analyzers)
- API contract: OpenAPI builds; client generation succeeds.
- Migration safety: EF migrations run in sample environment.
- “Clean state” requirement: the repo is always mergeable after each iteration.

## 7. Milestones (incremental delivery)
### M0 — Repo rebrand + monorepo skeleton (Octopus naming everywhere)
- Rename packages/namespaces/repo artifacts to Octopus.*
- Ensure build passes.

### M1 — Server baseline (health + swagger) + Aspire AppHost skeleton
- Octopus.Server.App runs; OpenAPI available.
- Octopus.AppHost runs both server and web shell.

### M2 — Workspaces/Projects/Users + RBAC
- EF schema, CRUD endpoints, membership enforcement.

### M3 — Files subsystem v1 (upload + commit + registry + usage endpoints)
- LocalDisk storage provider.
- Stream download endpoint.
- Usage reporting per workspace/project.

### M4 — Models/Versions v1
- Create model, create version from IFC FileId.
- Status states: Pending/Processing/Ready/Failed.

### M5 — Processing pipeline v1 (IFC → WexBIM)
- Background worker queue (in-memory channel).
- Artifact File created; FileLink created; ModelVersion updated.
- WexBIM streaming endpoint.

### M6 — Properties extraction v1
- Properties artifact produced.
- Query endpoint supports paging and filtering.

### M7 — Generated client package
- Octopus.Server.Client generation wired to build; package produced.

### M8 — Octopus.Blazor server integration
- Wrapper services implemented; UI components call services only.
- End-to-end sample: upload → process → view.

### M9 — Templates + CLI (Aspire-first DX)
- `dotnet new octopus` produces working solution.
- CLI optional but recommended (dotnet tool).

### M10 — Azure Blob storage provider + direct uploads (scale)
- SAS/direct upload flow.
- Commit verifies checksum/size where feasible.

## 8. Detailed requirements
### 8.1 Workspaces/Projects
- Create/list/read/update
- Invite flow (token-based acceptance)
- Membership management endpoints
- Role enforcement on all project/file/model endpoints

### 8.2 Files (CDE foundation)
- UploadSession reserve → upload content → commit
- File metadata: kind/category/content-type/size/checksum/provider/key
- File lineage: derived relationships
- Soft delete; usage excludes deleted
- Quota config (workspace-level) with enforcement option

### 8.3 Models/Versions and artifacts
- ModelVersion references:
  - IfcFileId
  - WexBimFileId (artifact)
  - PropertiesFileId (artifact)
- Processing jobs update version status and error details

### 8.4 Observability (baseline)
- Health checks
- Structured logs (correlation id)
- Minimal metrics hooks (optional early)

## 9. Deliverables for the long-running agent harness
The repo must contain:
- docs/PRD.md (this file)
- docs/feature_list.json (structured feature list with `passes: false` initially)
- docs/claude-progress.txt (append-only progress log)
- init.sh (dev bootstrap: start services + smoke test)
- ralph script (see `ralph.sh`)

## 10. Definition of Done (project-level)
- A developer can run Octopus.AppHost and:
  - create workspace/project
  - upload IFC
  - process to generate WexBIM + properties
  - view in Octopus.Web using Octopus.Blazor components
  - see storage usage for workspace/project
- Client generation and UI wrappers are working and documented.
- Each milestone is validated by automated checks and committed incrementally.
