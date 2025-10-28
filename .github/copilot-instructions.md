# Instructions for Copilot Agent (Serena / MCP)

Use the Serena MCP server to interact with this repository.

Primary action:

Onboarding & memory:
- After activation, check onboarding status.
- If onboarding is not yet performed, run the onboarding flow and write these memories:
  - `project_overview.md` — purpose, tech stack, key files, entrypoints
  - `suggested_commands.md` — build/run/test/publish/CI commands
  - `code_conventions.md` — naming/DI/MVVM/testing conventions and any formatting guidance
- Only write memories that summarize high-level, non-sensitive information.

Search & reading policy:
- Prefer symbol overview and targeted symbol reads for code understanding.
- Avoid reading entire large binary or build artifact directories (e.g., `bin/`, `obj/`, `artifacts/`) unless necessary.
- Use fast file listing/search first to find candidate files; then read small, focused sections.

Preferred tooling & behaviors:
- Prefer the following MCP tools in this order for code tasks: find_file / get_symbols_overview / find_symbol / find_referencing_symbols / read_file (targeted).
- When making edits, use project-consistent style and limit changes to minimal diffs; always produce a short summary of edits before applying them.
- If unsure about design/coding choices, ask the user rather than guessing.

CI / tests:
- This is a WPF (.NET 8) Windows-targeted project. Recommend `runs-on: windows-latest` for CI builds/tests.
- Skip or separate GUI-dependent tests in CI; document skip criteria.

Security & privacy:
- Do not exfiltrate secrets or environment-specific data.
- Do not write or commit generated credentials or local machine paths into the repo.

When to prompt the user:
- Before applying edits that change >1 file or touch build/test infrastructure.
- When memory writes would include potentially-sensitive or environment-specific content.

Example first-step sequence the agent should follow:
1. activate_project -> E:\Munition_AutoPatcher_vC
2. check_onboarding_performed
3. if not onboarded: run onboarding and write the three memory files above
4. run get_current_config and get_symbols_overview for key files (MunitionAutoPatcher/App.xaml.cs, MunitionAutoPatcher/MunitionAutoPatcher.csproj, tests/)
5. summarize planned edits to the user and wait for confirmation

Append DECISIONS.md summarization command (prompt templates)
-- Purpose: provide Copilot with ready-to-use prompts so it can summarize chat logs and append the result to `DECISIONS.md` in the repository.

Usage guidance for the agent:
- If the user asks to "summarize chat and append to DECISIONS.md" or similar, use one of the templates below depending on requested detail, generate the requested summary, then append it to `DECISIONS.md` with a clear header including timestamp and source (e.g., "# Chat summary — YYYY-MM-DD HH:MM").
- Before appending, present the generated summary to the user for confirmation if the change touches more than one file or is longer than ~500 characters.

Prompt templates (paste the chat log after the prompt marker when invoking):


# Instructions for Copilot Agent (Serena / MCP)

Use the Serena MCP server to interact with this repository.

Primary action
- Activate the project at E:\\Munition_AutoPatcher_vC using the MCP `activate_project` tool as the first step.

Onboarding & memory
- After activation, call `check_onboarding_performed`.
- If onboarding is not performed, run `onboarding` and write the following high-level memory files (non-sensitive):
  - `project_overview.md` — project purpose, tech stack, key files, entrypoints
  - `suggested_commands.md` — build/run/test/publish/CI commands and common developer workflows
  - `code_conventions.md` — coding style, DI/MVVM conventions, testing guidance

Search & reading policy
- Prefer symbol overview and targeted symbol reads for code understanding (use `get_symbols_overview` / `find_symbol`).
- Use `find_file` and fast file listing before reading file contents. Avoid reading entire large binary or build-artifact directories (`bin/`, `obj/`, `artifacts/`).

Preferred tools & order
1. `find_file`
2. `get_symbols_overview`
3. `find_symbol`
4. `find_referencing_symbols`
5. `read_file` (targeted)

Editing policy
- When editing, keep diffs minimal and follow existing project style. Present a short summary of planned edits to the user and wait for confirmation before applying changes that affect more than one file.

CI / tests guidance
- This is a WPF (.NET 8) Windows-targeted project. Prefer `runs-on: windows-latest` for CI build and test jobs.
- Separate or skip GUI-dependent tests in CI; document criteria for skipping.

Security & privacy
- Do not exfiltrate secrets, local absolute paths, or environment-specific credentials. If chat logs contain sensitive information, redact it or ask the user before appending.

When to prompt the user
- Prompt before applying edits that change multiple files or touch CI/test infra.
- Prompt before writing any memory that might include environment-specific content.

Recommended first-step sequence
1. `activate_project` -> E:\\Munition_AutoPatcher_vC
2. `check_onboarding_performed`
3. If not onboarded: `onboarding` and write the three memory files above
4. `get_current_config` and `get_symbols_overview` for key files (e.g., `MunitionAutoPatcher/App.xaml.cs`, `MunitionAutoPatcher/MunitionAutoPatcher.csproj`, `tests/`)
5. Summarize planned edits to the user and wait for confirmation

Append to `DECISIONS.md` — chat-summary prompt templates
Purpose: provide ready-to-use prompt templates so the agent can summarize chat logs and append the result to `DECISIONS.md` with a timestamp and clear header.

Usage guidance
- When asked to "summarize chat and append to DECISIONS.md", ask which template the user prefers (standard/short/detailed) if unspecified.
- Run the chosen template with the chat log, produce the summary, and show it to the user for confirmation if the append is longer than ~500 characters or affects multiple files.
- On confirmation, append a clearly marked section to `DECISIONS.md` with timestamp (e.g., `# Chat summary — 2025-10-28 15:00 UTC`) and the generated content.

Prompt templates (paste the chat log after the prompt marker when invoking):

1) 標準（推奨：200〜300文字の要約＋決定事項＋未解決タスク）

"以下に貼るチャットログを読み、次の形式で出力してください。

200〜300文字の要約（日本語）

本番で参照すべき『決定事項』を箇条書き（各1行）

未解決タスク（担当推定＋優先度を含めて箇条書）

参照すべきファイル／コード箇所（見つかればパス）

次の推奨アクション（3つまで）

不要な会話や重複は省いてください。ここからチャットログを貼ります："

2) 短縮（素早く確認したい時：1行要約＋3つの優先タスク）

"以下のチャットを読み、次を出してください：

1行（〜40文字）の要約

重要な決定事項（箇条書）

今すぐ着手すべき上位3タスク（優先度順、各タスクに1行コメント）

チャットログを貼ります："

3) 詳細（長期プロジェクト向け：トピック別要約＋時系列＋意思決定履歴）

"以下のチャットから、次を出してください：
A. トピックごとの短い要約（各トピック50〜120文字）
B. 時系列の重要イベント／決定（日時があれば付記）
C. すでに合意された仕様／非採用になった案の一覧
D. 未解決の技術的リスクと推奨対応（優先度つき）
E. 参照すべきファイル・行番号（見つかれば）

チャットログを貼ります："

Example workflow for appending a summary
1. Confirm which template to use.
2. Run the prompt and generate the summary.
3. Present the generated summary to the user for review.
4. On user approval, append to `DECISIONS.md` with a timestamped header.

If you want different templates or localization (e.g., English-only outputs), update this file.