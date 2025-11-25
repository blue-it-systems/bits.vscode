# AdvancedGenerator - Gengora Sample Project (Advanced)

This advanced sample demonstrates the generator ↔ server protocol in more detail.

Features:

- Reads JSON input from stdin (the extension will pass input to the generator)
- Emits `generator/status` and `generator/file` messages to stdout so the extension/server can track progress and emitted files
- Produces timestamped generated files so you can verify activity

Usage:

```bash
dotnet run --project AdvancedGenerator.csproj
```

You can also test with input via stdin:

```bash
echo '{ "command": "add", "message": "hello" }' | dotnet run --project AdvancedGenerator.csproj
```

Or pass files format used by the extension:

```bash
echo '{ "files": [{ "path": "src/MyClass.cs", "content": "public class MyClass { }" }] }' | dotnet run --project AdvancedGenerator.csproj
```

This sample prints a JSON line for each important event so you can see the exact contract the generator and server use to communicate.
