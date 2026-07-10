
# BallastLane — Task Management System

A full-stack task management application with JWT-authenticated users, per-user task ownership, and a Clean Architecture backend paired with an Angular standalone frontend.

## Overview

BallastLane lets users register, sign in, and manage personal tasks. Each task has a title, optional description, due date, and lifecycle status (`Pending`, `InProgress`, `Completed`). The API enforces validation rules and status transitions in the Application and Domain layers; the UI consumes REST endpoints under `/api/v1`.

A **Task Assistant** (`/task-assistant`) lets authenticated users manage tasks in natural language. The backend uses LLM function calling to invoke predefined tools (`create_task`, `list_tasks`, `get_task`, `update_task`, `delete_task`) that delegate to `ITaskService`.

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

