# API quick reference

All care endpoints require `Authorization: Bearer <access-token>`. Queries are restricted to care circles where the signed-in user is a member.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Create an account |
| POST | `/api/auth/login` | Issue access/refresh tokens |
| POST | `/api/auth/refresh` | Rotate a refresh token |
| GET | `/api/dashboard` | Today overview |
| GET/POST | `/api/tasks` | List/create shared tasks |
| PUT | `/api/tasks/{id}/complete` | Complete a task |
| GET/POST | `/api/medications` | List/create medication |
| POST | `/api/medications/{id}/logs` | Record taken/skipped dose |
| GET/POST | `/api/appointments` | List/create appointments |
| GET | `/api/conversations` | List care-circle conversations |
| GET | `/api/conversations/{id}/messages` | Message history |
| POST | `/api/messages` | Send message |
| GET/POST | `/api/handoffs` | List/create handoffs |
| PUT | `/api/handoffs/{id}/accept` | Accept responsibility |
| GET/POST | `/api/groceries` | List/add recipient grocery items |
| GET/PUT/DELETE | `/api/groceries/{id}` | View, edit, reassign, or remove an item |
| PUT | `/api/groceries/{id}/accept` | Accept a pending grocery assignment |
| PUT | `/api/groceries/{id}/decline` | Decline a pending grocery assignment |
| PUT | `/api/groceries/{id}/reassign` | Assign an item to another active care-circle member |
| PUT | `/api/groceries/{id}/toggle-purchased` | Mark an item purchased or restore it |
| POST | AI `/api/ai/care-summary` | Draft reviewed summary |
| POST | AI `/api/ai/handoff-draft` | Draft handoff fields |
