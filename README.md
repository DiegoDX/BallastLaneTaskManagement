
# BallastLane — Task Management System

A full-stack task management application with JWT-authenticated users, per-user task ownership, and a Clean Architecture backend paired with an Angular standalone frontend.

## Overview

BallastLane lets users register, sign in, and manage personal tasks. Each task has a title, optional description, due date, and lifecycle status (`Pending`, `InProgress`, `Completed`). The API enforces validation rules and status transitions in the Application and Domain layers; the UI consumes REST endpoints under `/api/v1`.

A **Task Assistant** (`/task-assistant`) lets authenticated users manage tasks in natural language. The backend uses LLM function calling to invoke predefined tools (`create_task`, `list_tasks`, `get_task`, `update_task`, `delete_task`) that delegate to `ITaskService`.

A **Doc Assistant** (`/doc-assistant`) answers questions about project documentation using RAG (Retrieval-Augmented Generation). On startup, the API indexes files from `BackEnd/Api/Documentation/`, embeds text chunks, and retrieves relevant context for each question before calling the LLM.

An **Agent** (`/agent`) runs a multi-phase workflow (Plan → Approval → Execute → Review → Summary) to organize and manage tasks in natural language. The Execute phase discovers and runs tools via the **TaskAssistant MCP Server** (stdio).

A **TaskAssistant MCP Server** exposes 10 task-domain tools (`create_task`, `update_task`, `delete_task`, `search_tasks`, `complete_task`, plus analytics/planning tools) for Cursor and the Agent Execute phase.

On startup (non-Testing environments), the API automatically creates the SQL Server database if needed, applies the schema, and runs seed data including a default admin account.

## Architecture

### Backend (Clean Architecture)

### Frontend
Angular standalone components with lazy-loaded routes, reactive forms, and an HTTP interceptor that attaches the JWT and redirects to login on `401`.

## Technologies

| Area | Stack |
|------|-------|
| Backend runtime | .NET 10 |
| API | ASP.NET Core Web API |
| Data access | ADO.NET (`Microsoft.Data.SqlClient`) — no EF Core |
| Auth | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Password hashing | PBKDF2-SHA256 (100,000 iterations) |
| API docs | ASP.NET Core OpenAPI (`MapOpenApi`) |
| Frontend | Angular 18 (standalone components) |
| Database | SQL Server |
| Tests | xUnit, Moq, FluentAssertions, `WebApplicationFactory` integration tests |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS recommended)
- SQL Server (LocalDB, Express, or full instance)
- Angular CLI (`npm install -g @angular/cli`) — optional; `npm start` uses the local CLI

## Running the Backend

1. Update the connection string in `BackEnd/Api/appsettings.json` (or `appsettings.Development.json`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=BallastLane;Integrated Security=True;TrustServerCertificate=True"
}
``` 

2.  Configure JWT settings in the same file
3. Run the API:
cd BackEnd/Api
dotnet run --launch-profile https

Default URLs (from launchSettings.json):

HTTPS: https://localhost:7194
HTTP: http://localhost:5098
On first run, DatabaseInitializer creates the BallastLane database, applies schema.sql, and runs seed.sql.

Seeded account (from seed.sql):

Username	Password
admin
Admin123!

## Running the Frontend
Install dependencies:
```json
cd Frontend/UI
npm install
``` 
Point the UI at the API in src/environments/environment.ts:
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7194/api/v1',
};
Start the dev server:
npm start
The app runs at http://localhost:4200 (Angular default). Routes:

| Path | Description |
|------|-------------|
| `/login` | Sign in |
| `/register` | Create account |
| `/tasks` | Task list (auth required) |
| `/tasks/new` | Create task |
| `/tasks/edit/:id` | Edit task |
| `/task-assistant` | AI task assistant — create, list, and manage tasks in natural language (auth required) |
| `/doc-assistant` | Doc assistant — ask questions about project documentation (auth required) |
| `/agent` | Agent — plan, execute, and review task changes with AI (auth required) |

Integration tests use separate config in BackEnd/Tests/appsettings.integration.json and appsettings.api.integration.json.


## API Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/auth/register` | No | Register user |
| POST | `/api/v1/auth/login` | No | Login, receive JWT |
| GET | `/api/v1/tasks` | Yes | List current user's tasks |
| GET | `/api/v1/tasks/{id}` | Yes | Get task by ID (owner only) |
| POST | `/api/v1/tasks` | Yes | Create task |
| PUT | `/api/v1/tasks/{id}` | Yes | Update title, description, status |
| DELETE | `/api/v1/tasks/{id}` | Yes | Delete task |
| POST | `/api/v1/task-assistant` | Yes | Natural-language task assistant (function calling) |
| POST | `/api/v1/doc-assistant` | Yes | Ask questions about indexed project documentation (RAG) |
| POST | `/api/v1/doc-assistant/reindex` | Yes | Rebuild the documentation vector index |
| POST | `/api/v1/agent` | Yes | Run the task agent workflow |
| POST | `/api/v1/agent/continue` | Yes | Approve or reject a pending agent plan |

## Task Assistant

The Task Assistant is available at **`/task-assistant`** in the UI (link from the task list) or via **`POST /api/v1/task-assistant`**. Send a conversation history; the API returns assistant text plus any task actions performed (created, listed, updated, deleted).

**Example request:**

```json
POST /api/v1/task-assistant
{
  "messages": [
    { "role": "user", "content": "Create a task \"Buy milk\" due tomorrow" }
  ]
}
```

**Example response:**

```json
{
  "content": "Done — I created Buy milk for tomorrow.",
  "model": "llama3.2",
  "actions": [
    {
      "type": "created",
      "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Buy milk",
      "dueDate": "2026-07-10T00:00:00Z"
    }
  ]
}
```

### Example prompts

- `Create a task "Buy milk" due tomorrow`
- `Add a task to prepare the demo for next Friday`
- `Create a task "Call dentist" due today`
- `Show my pending tasks`
- `Mark "Buy milk" as completed`
- `Delete the task "Call dentist"` (the assistant asks for confirmation before deleting)

Relative dates such as *today*, *tomorrow*, and *next Friday* are converted to ISO `YYYY-MM-DD` before calling tools.

### LLM configuration

Task Assistant requires an LLM provider that supports **function calling** (tools). Configure the `Llm` section in `BackEnd/Api/appsettings.json` (production) or `appsettings.Development.json` (local dev).

| Environment | Default provider | Notes |
|-------------|------------------|-------|
| Production (`appsettings.json`) | OpenAI (`gpt-4o-mini`) | Set `Llm:ApiKey`. Function calling works out of the box. |
| Development (`appsettings.Development.json`) | Ollama (`llama3.2` at `http://localhost:11434`) | Requires a local Ollama instance and a **tools-capable model**. |

### Ollama (local development)

When `Llm:Provider` is `Ollama`:

1. Install and run [Ollama](https://ollama.com/).
2. Pull a model with **function-calling / tools** support — for example `llama3.1`, `llama3.2`, or `mistral`. Older models without tool support will not invoke task tools reliably.
3. Match `Llm:Model` in `appsettings.Development.json` to the pulled model (default: `llama3.2`).

```bash
ollama pull llama3.2
ollama serve
```

If the model does not support tools, the assistant may respond with text only and no task actions will run. Use OpenAI in production or switch to a supported Ollama model for local testing.

## Doc Assistant

The Doc Assistant is available at **`/doc-assistant`** in the UI (link from the task list) or via **`POST /api/v1/doc-assistant`**. Send a conversation history; the API retrieves relevant documentation chunks, injects them into the prompt, and returns an answer with **source citations**.

**Example request:**

```json
POST /api/v1/doc-assistant
{
  "messages": [
    { "role": "user", "content": "How does authentication work?" }
  ]
}
```

**Example response:**

```json
{
  "content": "Authentication uses JWT Bearer tokens. Clients obtain a token via POST /api/v1/auth/login and send it in the Authorization header on protected endpoints.",
  "model": "llama3.2",
  "sources": [
    {
      "fileName": "README.md",
      "chunkIndex": 5,
      "excerpt": "Auth | JWT Bearer token authentication. Clients obtain an access token by calling the login endpoint..."
    },
    {
      "fileName": "Requirements.docx",
      "chunkIndex": 2,
      "excerpt": "All task endpoints require authentication. Each user may only access their own tasks..."
    }
  ]
}
```

### Example prompts

- `How does authentication work?`
- `What is the project architecture?`
- `What are the API endpoints?`

### Documentation folder

Indexed files live under **`BackEnd/Api/Documentation/`** (copied to the API output directory at build time). Configure the folder name via `Rag:DocumentationPath` in appsettings (default: `Documentation`).

Supported formats:

| Extension | Loader |
|-----------|--------|
| `.md` | Markdown |
| `.pdf` | PDF (UglyToad.PdfPig) |
| `.docx` | Word (DocumentFormat.OpenXml) |

Sample files included with the project:

- `README.md` — authentication, API overview, JWT interceptor
- `CleanArchitecture.pdf` — Domain / Application / Infrastructure / API layers
- `Requirements.docx` — business rules and auth requirements

On startup (non-Testing environments), `RagIndexHostedService` chunks each file (~800 characters with overlap), generates embeddings, and stores vectors in an in-memory index. If Ollama is unavailable at startup, the API still runs but Doc Assistant requests return `502`/`503` until indexing succeeds. Trigger a manual rebuild with **`POST /api/v1/doc-assistant/reindex`**.

### LLM and embedding configuration

Doc Assistant uses the same `Llm` provider as Chat and Task Assistant for the final answer, plus a separate **embedding model** for vector search. Set both in `BackEnd/Api/appsettings.json` or `appsettings.Development.json`:

```json
"Llm": {
  "Provider": "Ollama",
  "Model": "llama3.2",
  "EmbeddingModel": "nomic-embed-text",
  "BaseUrl": "http://localhost:11434"
},
"Rag": {
  "DocumentationPath": "Documentation"
}
```

| Environment | Chat model | Embedding model |
|-------------|------------|-----------------|
| Production (`appsettings.json`) | OpenAI `gpt-4o-mini` | OpenAI `text-embedding-3-small` |
| Development (`appsettings.Development.json`) | Ollama `llama3.2` | Ollama `nomic-embed-text` |

### Ollama embeddings (local development)

When `Llm:Provider` is `Ollama`, pull both the chat model and the embedding model:

```bash
ollama pull llama3.2
ollama pull nomic-embed-text
ollama serve
```

Ensure `Llm:EmbeddingModel` matches the pulled embedding model (default: `nomic-embed-text`). Without it, startup indexing and Doc Assistant queries fail with `502 Bad Gateway` or `503 Service Unavailable`. After pulling the model, restart the API or call `POST /api/v1/doc-assistant/reindex`.

## Agent

The Agent is available at **`/agent`** in the UI (link from the task list) or via **`POST /api/v1/agent`**. Unlike Task Assistant's single ReAct loop, the Agent runs a **deterministic multi-phase workflow** coordinated by `AgentOrchestrator` through specialized agents:

| Agent | Phase | Responsibility |
|-------|-------|----------------|
| `PlannerAgent` | Plan | Analyze objective, produce structured plan |
| *(Approval gate)* | Approval | Human-in-the-loop coordination (not an LLM agent) |
| `ExecutorAgent` | Execute | MCP tool-calling loop |
| `ReviewerAgent` | Review | Evaluate execution; may request re-execution |
| `SummaryAgent` | Summary | User-facing summary |

Specialized agents communicate through typed DTOs only and use **`ILlmClient`** as the provider-independent AI chat abstraction (equivalent to `IAIChatService`). The orchestrator sequences agents via thin phase adapters implementing `IAgentPhaseHandler`.

1. **Plan** — `PlannerAgent` produces a structured JSON plan
2. **Approval** — pauses when the plan is destructive or high-risk
3. **Execute** — `ExecutorAgent` runs an agentic tool-calling loop via MCP (`IMcpToolClient`)
4. **Review** — `ReviewerAgent` evaluates execution; may trigger another Execute pass (up to `Agent:MaxReExecutionAttempts`)
5. **Summary** — `SummaryAgent` produces the user-facing summary with phase timeline

**Example request:**

```json
POST /api/v1/agent
{
  "messages": [
    { "role": "user", "content": "Organize my tasks by due date" }
  ]
}
```

**Example response (completed):**

```json
{
  "summary": "I listed your pending tasks and updated statuses by due date.",
  "status": "Completed",
  "phases": [
    { "phase": "Plan", "status": "Completed", "outputJson": "{...}", "durationMs": 1200 },
    { "phase": "Approval", "status": "Skipped", "outputJson": null, "durationMs": 0 },
    { "phase": "Execute", "status": "Completed", "outputJson": "{...}", "durationMs": 4500 },
    { "phase": "Review", "status": "Completed", "outputJson": "{...}", "durationMs": 800 },
    { "phase": "Summary", "status": "Completed", "outputJson": "{...}", "durationMs": 600 }
  ],
  "actions": [
    { "type": "listed", "taskId": null, "title": null },
    { "type": "updated", "taskId": "...", "title": "Study history", "status": "InProgress" }
  ],
  "executionReport": {
    "iterations": 3,
    "toolCalls": [
      { "name": "search_tasks", "success": true },
      { "name": "update_task", "success": true }
    ]
  },
  "model": "llama3.2"
}
```

When a plan requires approval, the first response returns `"status": "AwaitingApproval"` with a `runId` and `plan`. Approve or reject via:

```json
POST /api/v1/agent/continue
{
  "runId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "approved": true
}
```

### Example prompts

- `Organize my tasks by due date`
- `Tomorrow I need to study history — set up my tasks`
- `Show pending tasks and update overdue ones to InProgress`

### Agent configuration

Configure limits in `BackEnd/Api/appsettings.json` or `appsettings.Development.json`:

```json
"Agent": {
  "MaxExecuteIterations": 10,
  "MaxToolCallsPerIteration": 5,
  "MaxPhaseRetries": 2,
  "RunTtlMinutes": 30,
  "RequireApprovalForDestructiveActions": true,
  "BulkUpdateApprovalThreshold": 3,
  "MaxReExecutionAttempts": 2
}
```

The Agent reuses the same `Llm:Provider` and `Llm:Model` as Chat and Task Assistant. For local development with Ollama, use a **tools-capable model** (default: `llama3.2`):

```bash
ollama pull llama3.2
ollama serve
```

Plans that include `delete_task`, bulk updates above the threshold, or `riskLevel: high` require explicit user approval before execution.

## TaskAssistant MCP Server

The Agent Execute phase spawns a **stdio MCP server** that exposes task tools to the LLM. Task Assistant still calls `ITaskToolExecutor` directly in MVP; only the Agent uses MCP.

### MCP tools

| Tool | Purpose |
|------|---------|
| `create_task` | Create a task |
| `update_task` | Update title, description, or status |
| `delete_task` | Delete a task (requires Agent approval) |
| `search_tasks` | Search by id, status, or title |
| `complete_task` | Mark a task Completed |
| `get_task_statistics` | Counts by status, overdue, due today |
| `generate_study_plan` | LLM study plan; optional task creation |
| `prioritize_tasks` | Suggested priority order |
| `summarize_progress` | Progress summary + metrics |
| `suggest_next_task` | Next open task by due date |

### API configuration

Configure the MCP client in `BackEnd/Api/appsettings.json`:

```json
"Mcp": {
  "ServerProjectPath": "../Mcp/BallastLane.TaskAssistant.Mcp.Server/BallastLane.TaskAssistant.Mcp.Server.csproj",
  "StartupTimeoutSeconds": 10,
  "SessionPerRequest": true
}
```

The API injects `BALLASTLANE_USER_ID` when spawning the server so tools run in the authenticated user's context. The LLM never receives `userId` in tool schemas.

### Cursor (development)

`.cursor/mcp.json` registers the same server for local testing in Cursor:

```json
{
  "mcpServers": {
    "ballastlane-tasks": {
      "command": "dotnet",
      "args": ["run", "--project", "BackEnd/Mcp/BallastLane.TaskAssistant.Mcp.Server/BallastLane.TaskAssistant.Mcp.Server.csproj"],
      "env": {
        "BALLASTLANE_USER_ID": "11111111-1111-1111-1111-111111111111"
      }
    }
  }
}
```

Use the seeded admin user id from `BackEnd/Infrastructure/Persistence/Scripts/seed.sql`. Ensure SQL Server is running and the API connection string matches the MCP server `appsettings.json` when testing tools that mutate data.

