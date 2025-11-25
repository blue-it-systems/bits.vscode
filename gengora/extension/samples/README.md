# Gengora Sample Generator Projects

This folder contains sample generator projects to help you get started with Gengora.

## Available Samples

### BasicGenerator

A minimal, well-documented generator project that demonstrates all the core concepts:

- Project marker configuration
- Type-safe communication with the Gengora server
- File emission protocol
- Status reporting
- Error handling

**Use Cases Demonstrated:**

- Generating boilerplate code
- Creating configuration files
- Auto-generating documentation

## Quick Start

1. Copy the `BasicGenerator` folder to your workspace
2. Open the folder in VS Code with Gengora installed
3. The generator will automatically be detected and compiled
4. Modify `Program.cs` to customize generation logic
5. Watch as changes are hot-reloaded!

## Creating Your Own Generator

1. Create a new .NET console application
2. Add `<IsGeneratorProject>true</IsGeneratorProject>` to the `.csproj`
3. Reference `Gengora.Generator.Abstractions` for type-safe development
4. Implement your generation logic
5. Use the `GengoraClient` class to communicate with the server

## Useful Use Cases for Code Generators

### 1. **Boilerplate Generation**

- CRUD operations for entities
- Repository pattern implementations
- DTO/ViewModel mappings

### 2. **Configuration & Constants**

- Environment-specific configuration classes
- Strongly-typed settings from JSON/YAML
- Enum generation from database values

### 3. **API Clients**

- REST API client code from OpenAPI specs
- GraphQL query builders
- gRPC client stubs

### 4. **Documentation**

- API documentation from code comments
- Markdown generation from XML docs
- README files from project structure

### 5. **Database**

- Entity classes from database schema
- Migration scripts
- Stored procedure wrappers

### 6. **Testing**

- Test fixture generation
- Mock implementations
- Test data builders

### 7. **Serialization**

- JSON serialization helpers
- Protocol buffer messages
- Binary serialization code

### 8. **Localization**

- Resource file generation
- Translation key constants
- Culture-specific formatters

## Best Practices

1. **Keep generators focused** - One generator per concern
2. **Use incremental generation** - Only regenerate what changed
3. **Validate inputs** - Check for required files/configuration
4. **Provide clear error messages** - Help users fix issues
5. **Document generated code** - Add comments explaining the source
6. **Handle edge cases** - Empty inputs, missing files, etc.

## Support

For questions or issues, please visit:
<https://github.com/blue-it-systems/bits.vscode/issues>
