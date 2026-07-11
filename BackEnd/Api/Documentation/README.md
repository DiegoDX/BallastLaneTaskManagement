# BallastLane API Documentation

## Authentication

BallastLane uses **JWT Bearer token** authentication. Clients obtain an access token by calling the login endpoint and send it on subsequent requests in the `Authorization` header.

### Login

```
POST /api/v1/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin123!"
}
```

A successful response returns a JWT access token and user metadata. The API also sets an HTTP-only refresh token cookie used to renew the access token.

### Protected endpoints

Controllers that require authentication are decorated with `[Authorize]`. Examples include:

- `GET/POST/PUT/DELETE /api/v1/tasks`
- `POST /api/v1/chat`
- `POST /api/v1/task-assistant`
- `POST /api/v1/doc-assistant`

Unauthenticated requests to these endpoints receive `401 Unauthorized`.

### Refresh and logout

- `POST /api/v1/auth/refresh` — issues a new access token using the refresh token cookie.
- `POST /api/v1/auth/logout` — revokes the refresh token and clears the cookie.

### Angular HTTP interceptor

The Angular frontend registers an `authInterceptor` that:

1. Attaches `Authorization: Bearer <token>` to outgoing API requests when a token is stored.
2. On `401`, attempts a silent refresh via `/api/v1/auth/refresh` and retries the original request.
3. Redirects to `/login` when refresh fails or the user is not authenticated.

Register and login endpoints skip the refresh flow.

## API overview

| Area | Base route | Auth required |
|------|------------|---------------|
| Auth | `/api/v1/auth` | No (except logout) |
| Tasks | `/api/v1/tasks` | Yes |
| Chat | `/api/v1/chat` | Yes |
| Task Assistant | `/api/v1/task-assistant` | Yes |
| Doc Assistant | `/api/v1/doc-assistant` | Yes |

## Task ownership

Every task belongs to a single user identified by `UserId`. The API resolves the current user from the JWT and ensures users can only access their own tasks.
