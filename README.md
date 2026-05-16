# wpf_single_word_count

Windows 11 向け WPF (.NET 8) 製の英単語学習デスクトップアプリ。

- 全体設計: [`docs/design.md`](docs/design.md)
- コーディングルール (反省と回避策): [`docs/coding-rules.md`](docs/coding-rules.md)

## 必要環境

- Windows 11
- .NET 8 SDK
- (任意) Visual Studio 2022 17.8 以降 / JetBrains Rider 2023.3 以降

## ソリューション構成

```
src/
  VocabApp.Core/           ドメインモデル, サービスインタフェース
  VocabApp.Infrastructure/ EF Core (SQLite) 永続化, LLM 実装 (将来)
  VocabApp.Wpf/            WPF アプリ本体 (View + ViewModel + DI)
tests/
  VocabApp.Core.Tests/
  VocabApp.Infrastructure.Tests/
```

## 開発フロー

### ビルドと実行

```powershell
dotnet build wpf_single_word_count.sln
dotnet run --project src/VocabApp.Wpf
```

初回起動時に `%AppData%\VocabApp\vocab.db` (SQLite) が自動生成される。

### テスト

```powershell
dotnet test wpf_single_word_count.sln
```

### EF Core マイグレーション

> Phase 0 では `Database.EnsureCreated()` でスキーマを直接作っている。
> スキーマが安定する Phase 1 以降に正式なマイグレーション運用に切り替える。

将来マイグレーションを有効化する際の初回コマンド:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate `
  --project src/VocabApp.Infrastructure `
  --startup-project src/VocabApp.Wpf
```

## ブランチ

開発は `claude/design-vocab-app-kh1g5` ブランチで進行中。

## ライセンス

未定 (個人利用)。
