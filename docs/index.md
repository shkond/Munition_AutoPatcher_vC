# Munition AutoPatcher vC ドキュメント

このドキュメントは DocFX によって自動生成されます。

概要:
- API ドキュメント: ソースの XML コメントから生成されます
- ガイド / アーキテクチャ: docs/ 以下の Markdown を元にビルドされます

ローカルでビルド:
```bash
# ビルド (Release を想定)
dotnet build MunitionAutoPatcher.sln -c Release

# docfx が入っていることが前提
docfx metadata docfx.json
docfx build docfx.json -o _site
```