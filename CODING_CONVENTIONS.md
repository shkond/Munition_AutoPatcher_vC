# Munition AutoPatcher vC - Coding Conventions

This document outlines the coding standards and best practices for the Munition AutoPatcher vC project. Adherence to these guidelines ensures code consistency, maintainability, and stability.

## 1. Naming Conventions

We follow standard .NET naming conventions with specific rules for this project.

### 1.1 General Rules
- **Classes, Structs, Enums, Delegates**: `PascalCase`
- **Interfaces**: `PascalCase` prefixed with `I` (e.g., `IMutagenAccessor`)
- **Methods, Properties, Events**: `PascalCase`
- **Parameters, Local Variables**: `camelCase`
- **Private Fields**: `_camelCase` (underscore prefix)
- **Constants**: `PascalCase` (not ALL_CAPS, unless external requirement)
- **Async Methods**: Suffix with `Async` (e.g., `InitializeAsync`)

### 1.2 Specific Patterns
- **ViewModels**: Suffix with `ViewModel` (e.g., `MainViewModel`)
- **Services**: Suffix with `Service` (e.g., `ConfigService`)
- **Commands**: Suffix with `Command` (e.g., `SaveCommand`)

## 2. Formatting & Style

### 2.1 Layout
- **Indentation**: 4 spaces (no tabs).
- **Braces**: Allman style (braces on new lines).
  ```csharp
  if (condition)
  {
      // ...
  }
  ```
- **Namespaces**: Use file-scoped namespaces for new files to reduce nesting.
  ```csharp
  namespace MunitionAutoPatcher.ViewModels;
  
  public class MyClass ...
  ```
- **Usings**: Place at the top of the file, before the namespace.

### 2.2 Language Features
- **Var**: Use `var` when the type is obvious from the right-hand side (e.g., `var list = new List<string>();`). Use explicit types when it aids readability.
- **Nullability**: Enable nullable reference types (`<Nullable>enable</Nullable>`). Handle potential nulls defensively.
- **Async/Await**: Use `async`/`await` for I/O and long-running operations. Avoid `.Result` or `.Wait()`.

## 3. Architecture & Patterns

### 3.1 MVVM (Model-View-ViewModel)
- **View (XAML)**: Contains only UI definitions and bindings. No business logic in code-behind.
- **ViewModel**: Handles presentation logic, state, and commands. Must implement `INotifyPropertyChanged` (via `ViewModelBase`).
- **Model/Services**: Contains business logic and data access. UI-agnostic.

### 3.2 Dependency Injection (DI)
- Use Constructor Injection for all dependencies.
- Register services in `App.xaml.cs` (or bootstrapper) using `Microsoft.Extensions.DependencyInjection`.
- Avoid Service Locator pattern (e.g., accessing `App.Current.Services` from static contexts).

### 3.3 Mutagen Integration
- **Strict Boundary**: Never call Mutagen APIs directly from ViewModels or Views.
- **Accessor Pattern**: Use `IMutagenAccessor` (or specific strategy interfaces) to interact with Mutagen.
- **Versioning**: Do not pin Mutagen versions. Use capability detection (Detector pattern) if necessary.

### 3.4 Logging
- **Services**: Use `ILogger<T>` injected via constructor.
- **UI/General**: Use `IAppLogger` for user-facing logs and status updates.
- **Prohibited**: Do not use `Console.WriteLine` or `Debug.WriteLine` for production logging.

## 4. Testing

- **Unit Tests**: Write unit tests for Services and ViewModels. Mock dependencies using `Moq`.
- **Integration Tests**: Use for Mutagen interactions and complex workflows.
- **Snapshot Testing**: Use for verifying complex output generation (e.g., patch files).

## 5. AI-Assisted Development

When using AI tools (Cursor, Copilot, etc.), follow the **4-Stage Process** defined in `CONSTITUTION.md`:
1.  **API Selection**: Review proposed APIs (no code generation).
2.  **Design Agreement**: Agree on inputs, outputs, error handling, and disposal.
3.  **Spike**: Minimal code snippets to verify assumptions.
4.  **Implementation**: Full implementation based on agreed design.

**AI Constraints**:
- No Reflection/Dynamic unless strictly necessary and encapsulated.
- No direct Mutagen calls in UI layer.
- Always define a `DisposePlan` for heavy resources (GameEnvironment, LinkCache).
