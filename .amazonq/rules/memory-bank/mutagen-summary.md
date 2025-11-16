This file provides a concise, opinionated overview of the Mutagen library that this project relies on, summarizing its purpose, core concepts (type-safe records, load order modeling, WinningOverrides), supported games, and typical usage patterns for Bethesda mod workflows.
It distills the upstream README and documentation into actionable guidance for contributors by highlighting key architecture points and APIs commonly used here (for example GameEnvironment, FormKey lookups, and record iteration), along with compatibility and version notes relevant to development and review.
It also defines practical do/don't rules, common pitfalls, and quick references so both human maintainers and coding agents can make consistent decisions without rereading the entire upstream docs each time.
For deeper details or edge cases, refer to the official Mutagen documentation site and the canonical repository, which remain the single source of truth beyond this summary.

# product
Mutagen is a C# library for type-safe handling of esp/esm files from Bethesda games, providing APIs to manipulate records as compile-time types.

Key features include strong typing with IntelliSense, performance optimization through short-circuit evaluation, extensive code generation, abstraction of complex binary structures, load order analysis, and support for both high-level and low-level operations.

Primary use cases include automatic patch generation, conflict resolution, bulk record conversion, compatibility patch creation, data analysis, and tool development, supporting multiple titles including Skyrim and Fallout 4.

# guidelines
Naming conventions follow PascalCase for classes/methods/properties, camelCase with underscore prefix for private fields, and T-prefix for generics.

Public APIs require comprehensive XML documentation (param/returns/exception/summary, with usage examples as needed). Namespaces are organized by functional hierarchy with one primary class per file as the standard.

Design principles emphasize builder/fluent patterns, immutability, type safety, and progressive interface segregation. Memory efficiency is achieved through Span/ReadOnlyMemorySlice, lazy evaluation, and similar optimizations.

Exception handling uses domain-specific types with Try patterns, early validation, and wrapping to preserve context. Binary processing leverages unsafe code, memory pools, stream position management, and batch processing.

Caching strategies specify lazy initialization, memoization, WeakReference usage, and invalidation approaches. APIs ensure correctness through overloads, parameter objects, optional arguments, and constraints, with extension methods placed in dedicated namespaces.

Architecture emphasizes getter separation, single responsibility, and composition. Generated code maintains partial classes, attribute-driven generation, consistent naming conventions, and allows manual overrides. Testing includes AAA patterns, builders, parameterization, and validation of real data, performance, and compatibility.

Dependencies are minimized with explicit versioning, abstraction, and fallback strategies. Internal architecture enforces layered design, dependency injection, abstract dependencies, and circular dependency avoidance.

# tech
Language is C# (latest preview), targeting .NET 8/9/10. Uses MSBuild with Central Package Management, Nullable enabled, Implicit Usings enabled, and unsafe code permitted in performance-critical areas.

Key dependencies include Loqui (generation), Noggog.CSharpExt, StrongInject, Immutable collections, compression via SharpZipLib/LZ4, and game detection through GameFinder (Steam/GOG/Xbox).

Tooling includes GitVersion and SourceLink. Build configuration features embedded PDB, XML documentation, nullable warnings treated as errors, and centralized package output.

Common commands include build/test/pack/restore. Performance optimization leverages unsafe code, binary overlays, Span, and source generators. Quality assurance includes analyzers, SourceLink, and symbol packages.

