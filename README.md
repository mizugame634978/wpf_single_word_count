# wpf_single_word_count

Windows 11 向け WPF (.NET 8) 製の英単語学習デスクトップアプリ。
単語の登録 / タグ管理 / 4 モードのテスト / 習熟度トラッキング / CSV 入出力 /
外部 LLM (Gemini CLI / ChatGPT 等) との連携をサポート。

- 全体設計: [`docs/design.md`](docs/design.md)
- コーディングルール (反省と回避策): [`docs/coding-rules.md`](docs/coding-rules.md)

## 現状 (Phase 4 完了時点)

| 機能 | 内容 |
|------|------|
| 単語管理 | 追加 / 編集 / 削除 / 検索 / 4 種フィルタ (タグ / 習熟度 / 品詞 / 文字列) |
| タグ | `;` 区切り入力、`NOCASE` でケース無視のユニーク化 |
| CSV 入出力 | UTF-8 / RFC 4180、衝突解決 3 モード (スキップ / 上書き / 別レコード) |
| LLM 連携 (外部) | プロンプトを表示 → `Ctrl+C` → Gemini CLI 等で生成 → CSV をインポート |
| テスト機能 | 4 モード (英→和 / 和→英 / 4 択 / フラッシュカード)。E→J は自己採点 |
| 習熟度 | 正解 +1 / 不正解 -1 / 0〜5 クランプ。出題数・正答数・最終出題日も自動記録 |
| 設定 | テスト既定値、DB 初期化、ログフォルダ参照 (`%AppData%\VocabApp\`) |
| キーボード | `Ctrl+N` / `Ctrl+F` / `F5` / `Delete` / `Enter` / `Esc` |
| 例外処理 | グローバルハンドラ、`InnerException` までコピー可能ダイアログで表示 |

**保留中**: アプリ内 LLM 直接生成 (Phase 5)。外部 LLM 経路で代替できるため見送り。
詳細は `docs/design.md §3.6.2`。

## 必要環境

- Windows 11
- .NET 8 SDK
- (任意) Visual Studio 2022 17.8 以降 / JetBrains Rider 2023.3 以降

## ソリューション構成

```
src/
  VocabApp.Core/           ドメインモデル, サービスインタフェース, ユーティリティ
  VocabApp.Infrastructure/ EF Core (SQLite) 永続化, CSV, 設定 JSON
  VocabApp.Wpf/            WPF アプリ本体 (View + ViewModel + DI)
tests/
  VocabApp.Core.Tests/
  VocabApp.Infrastructure.Tests/
tools/
  gen_icon.py              アプリアイコン (`Resources/app.ico`) 再生成スクリプト
```

## 開発フロー

### ビルドと実行

```powershell
dotnet build wpf_single_word_count.sln
dotnet run --project src/VocabApp.Wpf
```

初回起動時に `%AppData%\VocabApp\` 配下に以下が自動生成される:

- `vocab.db` (SQLite, 単語と学習履歴)
- `settings.json` (設定)
- `logs\vocab-YYYYMMDD.log` (Serilog ログ)

### テスト

```powershell
dotnet test wpf_single_word_count.sln
```

### EF Core マイグレーション

> 現状は `Database.EnsureCreated()` でスキーマを直接作っている。
> スキーマ変更 (collation や FK の変更) は既存 DB に反映されないため、
> 設定画面の **「DB を初期化」** ボタンで再生成するか、`%AppData%\VocabApp\vocab.db`
> を削除してから再起動する。

将来マイグレーションを有効化する際の初回コマンド:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate `
  --project src/VocabApp.Infrastructure `
  --startup-project src/VocabApp.Wpf
```

## ブランチ運用

- `main` が安定ブランチ。
- 機能ごとに `claude/<phase or topic>` を切って PR でマージ済み。
- 履歴は PR #1〜#7 を参照。

## ライセンス

未定 (個人利用)。
