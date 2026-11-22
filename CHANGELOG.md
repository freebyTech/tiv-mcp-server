# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Solution scaffold: `tivMcp.Api` (ASP.NET Core, .NET 10, official MCP C# SDK) and `tivMcp.Api.Tests` (xUnit).
- Streamable HTTP MCP endpoint at `/mcp` and an unauthenticated `/health` probe.
- `get_server_info` tool for confirming a connection.
- Required `MCPAPP__*` configuration with fail-fast validation and no built-in defaults.
- Sign-in: the server is an OAuth protected resource. `/mcp` requires a bearer token issued by the configured authority for this server's own audience; unauthenticated calls are challenged with the protected resource metadata location.
- OAuth protected resource metadata at `/.well-known/oauth-protected-resource/mcp`, advertising the `data:read` and `data:write` scopes.
- `IUserTokenSource`, the single place that decides which token is sent to the trackItsValue API (currently the caller's own validated token).
- Read-only tools, each a pass-through to one trackItsValue API endpoint that returns the API's JSON unchanged: `list_budgets`, `get_budget`, `list_accounts`, `get_accounts_overview`, `get_user_status`, `get_total_wealth_outlook` and `export_data`.
- `ITivApiClient`, the one client tools use to call the trackItsValue API as the signed-in user.
- Refusals from the API (401, 402, 403, 404, 429 and server faults) are returned as clear tool errors that carry the API's own reason, without leaking server detail.
- Dockerfile (runs unit tests during the image build), Jenkinsfile and Helm chart.
