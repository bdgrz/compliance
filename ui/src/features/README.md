# Feature folders

Each product capability gets a vertical folder under `features/` when its first
user story is implemented. A feature owns its API adapter, state/model,
components, tests, and feature-specific pages. Shared shell and fallback routes
remain under `pages/`.

Promote code to `components/` only after it is genuinely shared across product
capabilities. Keep provider authentication in `features/authentication/`, and
keep authorization enforcement on the ASP.NET Core API even when a client route
also has an AskrJS access guard.
