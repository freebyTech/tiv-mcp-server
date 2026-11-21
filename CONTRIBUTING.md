# Contributing

Thanks for your interest in the trackItsValue MCP server.

## Ground rules

- **Nothing private in this repository.** No client secrets, connection strings, tenant-specific keys or internal hostnames. All configuration comes from environment variables; `.env.example` holds placeholders only.
- **Thin wrapper only.** This server handles sign-in, tool definitions, and passing calls through to the trackItsValue API with the user's own token. It contains no business logic and never touches a database. If a tool needs something the API cannot do, the API gets that endpoint first.
- **The API is the gatekeeper.** Levels and permissions are enforced by the trackItsValue API. Tools pass its 401/403 responses back as clear tool errors rather than re-implementing the rules.
- **Tests with every change.** Tool argument validation, request mapping, error mapping and auth metadata are unit tested, with HTTP calls to the API faked.

## Workflow

1. Create a feature branch from `develop`. Never push directly to `main` or `develop`.
2. Make your change with tests.
3. Format and verify:

   ```
   dotnet tool restore
   dotnet csharpier src
   dotnet test src/tivMcp.Api.sln
   ```

4. Add a line to `CHANGELOG.md` under **Unreleased**.
5. Open a pull request.

## Code style

- Two-space indentation, enforced by `.editorconfig` and CSharpier (`.csharpierrc.json`).
- Private fields are `_camelCase`.
- Tests use xUnit with hand-written fakes; no mocking library.

## License

By contributing you agree that your contributions are licensed under the MIT License.
