# コーディングルール

過去の実装で得られた反省を蓄積していくドキュメント。
新規ルールは原則として **「実際に起きた失敗 + 回避方法」** をワンセットで書く。
抽象的なベストプラクティス集にはしない。

## UI

### MessageBox は使わない (コピー可能な独自ダイアログを使う)

- **反省 (Phase 2)**: エラーメッセージや LLM プロンプト本文を `MessageBox.Show` で表示したが、ユーザがエラー文を選択・コピーできず、再現報告に支障が出た。
- **ルール**:
  - エラー / 情報 / 長い本文を表示する箇所では `MessageBox` を使わず、`src/VocabApp.Wpf/Views/ErrorDialog.xaml` (または同等の独自 Window) を経由する。
  - 本文は `IsReadOnly="True"` の `TextBox` に入れて、選択 / 折り返し / スクロールを許可する。
  - 開いた瞬間に `SelectAll()` + `Focus()` を呼び、ユーザが Ctrl+C だけで取得できる状態にする。
- **例外**: 純粋な Yes/No 確認 (削除確認等) は標準の `MessageBox` で十分。

### クリップボードへの自動書き込みを主経路にしない

- **反省 (Phase 2)**: 「プロンプトをクリップボードにコピー」を機能の主導線にしたが、ユーザ環境で `CLIPBRD_E_CANT_OPEN (HRESULT 0x800401D0)` が頻発した。RDP / クリップボードマネージャ / セキュリティソフトが常時ロックしているケースは 10 回 × 100ms のリトライでも解放されない。
- **ルール**:
  - 「ユーザに文字列を渡す」UI の正規ルートは **コピー可能な TextBox に表示** することにする (上記ダイアログ流用)。
  - クリップボード API (`Clipboard.SetText` / `SetDataObject`) は補助的なボタンとしてのみ提供し、失敗を握りつぶしても機能全体が止まらない設計にする。
  - クリップボード書き込みが失敗した場合は、本文を `SelectAll()` + `Focus()` してユーザを Ctrl+C へ誘導する (黙って失敗させない)。

### 自動判定が本質的に難しい問題は UX (自己採点) で逃がす

- **反省 (Phase 3)**: 英 → 和テストで「ユーザが入力した日本語訳」を
  CSV 記載の `meaning` と完全一致 (大小無視 / 複数訳の `;` 区切り含む)
  で判定したが、日本語訳の表記ゆれ (「放棄する」「放棄」「あきらめる」
  「諦める」「見捨てる」…) を全て CSV に書き出すのは非現実的で、
  正しい回答でも `×` になる事象が頻発した。
- **ルール**:
  - 意味の同値判定など、入力空間が広い問題は「ユーザに正解を見せて
    自己採点させる」UX (Anki 風) を最初に検討する。実装も軽い。
  - 「日本語の正規化」「形態素解析」「埋め込み類似度」など重い実装に
    取り掛かる前に、本当に自動化が必要か (= 自己採点で十分か) を問う。
  - 必要なら LLM 判定 (Phase 5 連携) を後付けの上位モードとして用意する。

## データ層

### SQLite 文字列比較は明示的に NOCASE を指定する

- **反省 (Phase 1 → Phase 2 で発覚)**: `Tag.Name` を `IsUnique()` にしただけでは `"TOEIC"` と `"toeic"` が別行として登録されていた。SQLite の既定 collation (`BINARY`) は大小区別あり。
- **ルール**:
  - 大小無視で同一視したいテキスト列には EF Core の `.UseCollation("NOCASE")` をプロパティに付ける。
  - ユニークインデックスもその collation で評価されるため、重複防止とサービス側からの再利用が両立する。
  - 必要に応じてサービス側でも `.Distinct(StringComparer.OrdinalIgnoreCase)` を併用し、入力段階でも揃える。

### EnsureCreated 期間中のスキーマ変更は既存 DB に反映されない

- **反省 (Phase 2)**: 上の NOCASE 修正を入れても、Phase 0/1 時代に作られた `vocab.db` には反映されない。
- **ルール**:
  - `EnsureCreated` を使っているフェーズでエンティティ設定 (列 collation / 制約 / インデックス) を変えたら、PR 本文と README に「`%AppData%\VocabApp\vocab.db` を削除する必要がある」旨を明記する。
  - 正式な EF Core migration に切り替える際、未反映のスキーマ変更をまとめて 1 つの InitialCreate もしくは追加 migration に取り込む。

## アーキテクチャ

### Core / Infrastructure に WPF 型を持ち込まない

- **ルール**:
  - ダイアログ表示・ファイル選択・クリップボード等の UI 副作用は `IDialogService` を経由する。
  - `VocabApp.Core` / `VocabApp.Infrastructure` プロジェクトに `using System.Windows;` 系を入れない。Roslyn analyzer は未導入なので PR 時に目視で確認。

### DbContext は IDbContextFactory で取り回す

- **反省 (Phase 1)**: `AddDbContext` (Scoped) で登録すると、Singleton な ViewModel から使った時にライフタイム衝突 (captive dependency) が起きた。
- **ルール**:
  - WPF アプリの DI は `services.AddDbContextFactory<VocabDbContext>(...)` で登録する。
  - サービスは `IDbContextFactory<VocabDbContext>` を受け取り、操作ごとに `await using var db = await factory.CreateDbContextAsync();` する。
  - サービス / ViewModel を Singleton 化しても問題ないようにする。

## Git / リリースフロー

### Phase ごとにブランチを切り、1 PR にまとめる

- **ルール**:
  - 各 Phase は `claude/phaseN-<topic>` ブランチで進める。
  - レビュー粒度を保つため、Phase をまたいだ作業を 1 PR にしない。
  - 設計ドキュメント (`docs/design.md`) と本ルール (`docs/coding-rules.md`) の更新は、関連する Phase の PR に含めて構わない。
