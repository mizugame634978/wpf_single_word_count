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

### クリップボードへの自動書き込みは UI に出さない

- **反省 (Phase 2)**: 「プロンプトをクリップボードにコピー」を機能の主導線にしたが、ユーザ環境で `CLIPBRD_E_CANT_OPEN (HRESULT 0x800401D0)` が頻発した。RDP / クリップボードマネージャ / セキュリティソフトが常時ロックしているケースは 10 回 × 100ms のリトライでも解放されない。
- **反省 (Phase 4)**: フォールバックとして残した「本文をコピー」ボタンも、同じ環境では基本失敗するため、押すと「コピー失敗」表示になり「壊れている UI」に見えた。
- **ルール**:
  - 「ユーザに文字列を渡す」UI の正規ルートは **コピー可能な TextBox に表示** することにする (上記ダイアログ流用)。
  - クリップボード API (`Clipboard.SetText` / `SetDataObject`) を **押せるボタンとして UI に出さない**。アプリから書き込みに行く UI 自体を提供しない。
  - 代わりに、TextBox を開いた瞬間に `SelectAll()` + `Focus()` し、ヒント文で「Ctrl+C でコピーできます」と明示してユーザを手動コピーへ誘導する。

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

### 例外表示は InnerException までチェーン全部を見せる

- **反省 (Phase 4)**: 単語削除時に `DbUpdateException: An error occurred
  while saving the entity changes. See the inner exception for details.`
  が出たが、UI には外側の `ex.Message` しか出していなかったため、ユーザに
  原因 (実際は FK 制約違反) が伝わらなかった。
- **ルール**:
  - VM のエラーメッセージは `VocabApp.Core.Utilities.ExceptionFormatter.Format(ex)`
    で組み立て、`InnerException` を末端までインデント表示する。
  - グローバル例外ハンドラは `FormatWithStack(ex)` で最深部のスタック
    トレースも添える。
  - 単独で `{ex.Message}` だけを出さない。

### EF Core の OnDelete はリレーション設計時に明示する

- **反省 (Phase 4)**: `TestAnswer.Word` の FK を `DeleteBehavior.Restrict`
  で構成していたため、一度テストに出した単語が削除できなくなっていた。
- **ルール**:
  - 「親が消えたら子はどうなるか」を必ず意思決定し、`OnDelete(...)` を
    省略しない (省略時の既定は非自明)。
  - Cascade / SetNull / Restrict のどれを選んだか、`VocabDbContext` の
    該当ブロックに 1 行コメントを残す。
  - 既存 DB のスキーマ変更が `EnsureCreated` で反映されない期間は、
    サービス側で「先に子レコードを消す」フォールバックを併用する。

### LLM プロンプトは失敗例で矯正する

- **反省 (アイコン PR 後)**: 「英単語の和訳を出して」と曖昧に頼んだら、
  `dictionary → ディクショナリ` のようなカタカナ音訳が混ざり、学習教材として
  使い物にならなかった。「日本語で書いて」と書いただけでは LLM は音訳も
  日本語と解釈する。
- **ルール**:
  - プロンプトに「やってほしいこと」だけでなく「**やってほしくないこと + 悪い例**」
    を明示的に並べる (✗ ディクショナリ / ✓ 辞書 のような対比形式)。
  - 出力に必須項目があれば「必ず入れる」と書く + お手本となる 1 行を添える。
    例示は最強の指示。
  - プロンプト変更時は `PromptTemplateServiceTests` のような単体テストで
    キーフレーズ (悪い例の単語 / 形式キー) の存在を assert し、削れて
    退行することを防ぐ。

### API キー等のシークレットは平文でディスクに書かない

- **ルール (Phase 5)**:
  - シークレットは `ISecretProtector.Protect()` で DPAPI 暗号化してから設定ファイルに書く。
  - 復号は使用時 (API 呼び出しの直前) に行い、長時間メモリに保持しない。
  - 設定ファイル上の暗号文を読める人 = そのユーザ自身に限定される (DPAPI の
    `DataProtectionScope.CurrentUser`)。
  - 単体テスト用には `ISecretProtector` を平文返しの実装で差し替える。

### 長寿命サービスから HttpClient を使うときは IHttpClientFactory 経由にする

- **反省 (Phase 5)**: 当初 `services.AddHttpClient<GeminiVocabularyGenerator>()` で
  HttpClient を直接コンストラクタ注入していたが、Generator を Singleton として
  扱いたい場面で captive dependency になることが分かった。
- **ルール**:
  - Singleton サービスが HTTP を使う場合は `IHttpClientFactory` を注入し、呼び出し
    ごとに `factory.CreateClient(name)` で取り出す。
  - 名前付きクライアントのタイムアウト等は DI 登録時に `AddHttpClient(name, ...)` で
    集約する。

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
