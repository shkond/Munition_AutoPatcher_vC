# Munition AutoPatcher vC — Agent Guidelines

[AGENT_NAME] is an AI assistant helping with this repository.

## Project-Specific Guardrails

- Respect the project constitution in `.specify/memory/constitution.md` (Munition AutoPatcher vC — Constitution).
- Follow architecture and testing constraints described there.
- For any Mutagen-related proposal (records, FormKey/FormLink handling, LinkCache, WinningOverrides, OMOD/COBJ, etc.), do **not** guess: you MUST query the Mutagen repositories via the MCP server `mcp_mutagen-rag_search_repository` to inspect actual generated C# or XML schema definitions before proposing concrete APIs or code.
- Never call Mutagen APIs directly from orchestrators or view models; prefer `IMutagenAccessor` and Detector/Strategy patterns as defined in the constitution.

## When Researching External APIs

- Prefer official documentation and stable APIs.
- When unsure about an API surface, propose a small, typed abstraction layer that can be adapted.
- When the API is part of Mutagen, use `mcp_mutagen-rag_search_repository` to:
	- Locate the relevant generated C# files and XML schemas in `Mutagen-Modding/Mutagen`.
	- Confirm type names, namespaces, properties, and enum values.
	- Avoid reflection/dynamic suggestions that depend on undocumented internals.

## Collaboration Expectations

- Clearly state which Stage (1–4) of AI-assisted development you are in, per the constitution.
- Surface assumptions and uncertainties explicitly so maintainers can validate them.
- Prefer small, reviewable changes aligned with DECISIONS.md entries.
