
# BallastLane — Task Management System

A full-stack task management application with JWT-authenticated users, per-user task ownership, and a Clean Architecture backend paired with an Angular standalone frontend.

## Overview

BallastLane lets users register, sign in, and manage personal tasks. Each task has a title, optional description, due date, and lifecycle status (`Pending`, `InProgress`, `Completed`). The API enforces validation rules and status transitions in the Application and Domain layers; the UI consumes REST endpoints under `/api/v1`.

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

| Path | Description|
|------|------------|
|/login          |  Sign in|
|/register       |  Create account|
|/tasks          |  Task list (auth required)|
|/tasks/new      |  Create task|
|/tasks/edit/:id |  Edit task|

Integration tests use separate config in BackEnd/Tests/appsettings.integration.json and appsettings.api.integration.json.


API Endpoints
|Method |	Route	| Auth |	Description|
|------|--------|------|-------------|
|POST|/api/v1/auth/register|No|Register user|
|POST|/api/v1/auth/login|No|Login, receive JWT|
|GET|/api/v1/tasks|Yes|List current user's tasks|
|GET|/api/v1/tasks/{id}|Yes|Get task by ID (owner only)|
|POST|/api/v1/tasks|Yes|Create task|
|PUT|/api/v1/tasks/{id}|Yes|Update title, description, status|
|DELETE|/api/v1/tasks/{id}|Yes|Delete task|

