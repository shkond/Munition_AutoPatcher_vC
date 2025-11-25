---
description: Mutagen RAG MCP Server 起動手順
---

## 手順
1. **uv がインストールされていることを確認**
   ```powershell
   uv --version
   ```
   インストールされていない場合は以下を実行してください。
   ```powershell
   pip install uv
   ```
2. **サーバーディレクトリへ移動**
   ```powershell
   Set-Location "C:/Users/kondo/Desktop/mutagen_rag"
   ```
3. **サーバーを起動**
   ```powershell
   uv --directory "c:/Users/kondo/Desktop/mutagen_rag" run server.py
   ```
   - このコマンドは `uv` がプロジェクトの依存関係を解決し、`server.py` を実行します。
   - 起動後、コンソールに `FastMCP server listening on ...` のようなメッセージが表示されれば成功です。
4. **MCP クライアントから接続**
   - クライアント設定で `http://localhost:PORT`（ポートはサーバーログに表示）を指定してください。

## 補足
- サーバーはバックグラウンドで実行したい場合は、PowerShell の `Start-Process` などで非同期に起動できます。
- ログは標準出力に出力されますが、必要に応じて `> server.log 2>&1` でファイルにリダイレクトしてください。

**注意**: 上記コマンドはファイルシステムに変更を加えません。実行はユーザーの確認が必要です。
