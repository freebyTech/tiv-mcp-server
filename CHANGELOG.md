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
- Read-only tools, each a pass-through to one trackItsValue API endpoint that returns the API's JSON unchanged: `list_budgets`, `get_budget`, `list_accounts`, `get_accounts_overview`, `get_user_status` and `get_total_wealth_outlook`.
- `ITivApiClient`, the one client tools use to call the trackItsValue API as the signed-in user.
- Refusals from the API (401, 402, 403, 404, 429 and server faults) are returned as clear tool errors that carry the API's own reason, without leaking server detail.
- Summary tools `get_budget_summary` and `get_accounts_summary`: each gets the API's JSON, hands it unchanged to the trackItsValue agent API and returns the short text summary it writes.
- `ITivAgentApiClient`, the one client tools use to call the trackItsValue agent API as the signed-in user, and the required `MCPAPP__TIVAGENTAPIBASEURL` setting.
- Budget editing tools that mirror the agent API's tools by name and arguments: `add_actual`, `update_actual`, `delete_actual`, `add_item`, `update_item`, `delete_item`, `add_category`, `update_category`, `delete_category`, `move_budget`, `update_estimates`, `rectify_item`, `rectify_category`, `rectify_budget` and the read-only `get_available_accounts`. The agent API applies the change and the trackItsValue API saves it; nothing is saved when either refuses.
- `includeTemplates` option on `get_budget_summary`, to list the category and item templates before adding one.
- When the API rejects a request as invalid (400), the tool error now names the fields and what is wrong with them.
- `get_accounts_summary` asks the agent API to leave out the template names and ids, roughly halving the summary.
- `get_accounts_summary` asks for the agent API's compact form: closed accounts at $0.00 all month with no transactions are counted, not listed, and an unchanged fund takes one line. Open accounts are always listed.
- Dockerfile (runs unit tests during the image build), Jenkinsfile and Helm chart.
