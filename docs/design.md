# 英単語学習アプリ デザインドキュメント

## 1. 概要

Windows 11 専用の WPF (.NET 8) デスクトップアプリ。単一ユーザ向け。
ユーザは単語帳の単語を登録・編集し、複数モードで自己テストを行える。
単語データは CSV でインポート / エクスポートでき、将来的には LLM による
単語生成・和訳生成にも対応する。

> **実装方針**: アプリ本体のコードは常に Claude (Claude Code) が書く。
> ただし「単語リストの中身」については Gemini CLI など別の LLM にも
> 生成させる可能性があるため、CSV を「LLM の出力を取り込む共通インタフェース」
> として位置づける (§3.6 参照)。

### 1.1 ゴール

- 自分専用の単語帳を継続的に育てられる UX を提供する
- CSV を介して他ツール (Anki, スプレッドシート等) と相互運用できる
- テスト結果から「覚えている / 覚えていない」を学習進捗として蓄積する
- LLM 連携を後付けで導入できる拡張性を持たせる

### 1.2 非ゴール (初期スコープ外)

- マルチユーザ / クラウド同期
- 音声認識・発音評価
- モバイル対応
- 共有用 SRS (Anki 互換のスケジューリングまでは作り込まない)

## 2. 想定ユースケース

| # | シナリオ |
|---|----------|
| U1 | 学習中の単語を 1 つずつ手動で登録する |
| U2 | 既存の CSV (教材から書き出したもの) をインポートして一気に取り込む |
| U3 | 単語帳全体・タグ別・苦手な単語のみでテストを実行する |
| U4 | テスト結果に応じて習熟度が自動更新される |
| U5 | 学習結果込みで CSV にエクスポートし、バックアップ / 共有する |
| U6 | (将来) 「TOEIC 700 レベルの動詞 20 個」のようにテーマ指定で LLM に生成させ追加する |

## 3. 機能要件

### 3.1 単語管理
- 単語の CRUD (追加 / 編集 / 削除 / 一覧)
- 一覧での絞り込み (キーワード, タグ, 習熟度, 品詞)
- 並び替え (追加日時 / 最終出題日 / 習熟度 / アルファベット順)
- 重複チェック (語形 + 品詞で同一視)

### 3.2 CSV 入出力
- インポート
  - 文字コード: UTF-8 (BOM 有無の双方を許容)
  - 区切り: カンマ。引用符は RFC 4180 準拠
  - ヘッダ行必須。列順は任意 (ヘッダ名で解決)
  - 不明な列は警告して無視。必須列が欠ける場合はエラー
  - 既存単語との衝突は「スキップ / 上書き / 別レコードとして追加」を選択
- エクスポート
  - フィルタ状態 (全件 / 表示中のみ / 選択中のみ) を選んで出力
  - 学習統計 (出題回数, 正答数, 最終出題日時, 習熟度) も含めるかを選択

### 3.3 CSV フォーマット (v1)

```
word,meaning,part_of_speech,example,tags,notes,times_asked,times_correct,last_asked_at,mastery
abandon,放棄する,verb,He abandoned the plan.,"toeic,verb",,12,9,2026-05-10T09:30:00Z,3
```

| 列名 | 必須 | 説明 |
|------|------|------|
| `word` | ◯ | 英単語 (1〜64 文字) |
| `meaning` | ◯ | 和訳。複数訳は `;` 区切り |
| `part_of_speech` | △ | `noun` / `verb` / `adj` / `adv` / `phrase` など。空欄可 |
| `example` | △ | 例文 (英語) |
| `tags` | △ | カンマ区切りタグを 1 セルに収めるため、セル内では `;` 区切り |
| `notes` | △ | 学習者メモ |
| `times_asked` | △ | 出題回数 (整数, 既定 0) |
| `times_correct` | △ | 正答回数 (整数, 既定 0) |
| `last_asked_at` | △ | 最終出題日時 (ISO 8601 UTC) |
| `mastery` | △ | 習熟度 0〜5 (Leitner ボックス相当) |

意図:
- 最低限 `word` と `meaning` だけあれば取り込めるようにする
- 学習統計列は欠けていても 0 / null で読み込めるようにし、他ツール由来の
  シンプルな CSV (単語と意味だけ) もそのまま使えるようにする

### 3.4 テスト機能

出題モード:
1. **英→和**: 英単語を見せて、和訳をテキスト入力
2. **和→英**: 和訳を見せて、英単語をテキスト入力
3. **4 択 (英→和)**: 同じタグ / 品詞からダミー選択肢を生成
4. **フラッシュカード**: 表/裏をめくり「覚えた / 怪しい」をユーザが自己申告

出題対象の選択:
- 単語帳全体 / タグ指定 / 「苦手 (正答率 < 70%)」 / 「未出題」/ 「最終出題から N 日経過」
- 出題数は 10 / 20 / 50 / すべて から選択

判定:
- テキスト入力時は **大文字小文字を区別せず**、前後空白を無視。複数訳は
  `;` のどれかに一致すれば正解 (緩判定)。ハイフン / ピリオドはそのまま比較
- セッション終了時に正答率と「間違えた単語」一覧を表示。間違えた単語のみで
  再テストする導線を用意

### 3.5 習熟度モデル (簡易 SRS)

`mastery` は 0〜5 の整数。
- 正解で +1 (上限 5)
- 不正解で -1 (下限 0) ※連続不正解で 0 に張り付かないようにペナルティ係数は無し
- 「苦手」フィルタは `mastery <= 1` または直近の正答率 < 70%

初期実装はこの単純ルールに留め、本格的な SM-2 等は将来検討。

### 3.6 LLM 連携

「単語リストの内容」は Claude だけでなく Gemini CLI など別の LLM にも
書かせる可能性があるため、本アプリの LLM 連携は **2 系統** を用意する。

#### 3.6.1 外部 LLM 取り込みモード (Phase 2 で同時対応)

外部の LLM (Gemini CLI, ChatGPT, Claude.ai など) で生成した結果を、
そのまま CSV としてアプリに取り込めるようにする。

- 「LLM 用プロンプトを出力」メニューから、§3.3 の CSV v1 フォーマットに
  沿って単語を生成させるプロンプトテンプレートをクリップボードにコピーできる
- ユーザは Gemini CLI 等にそのプロンプトを渡し、結果の CSV をファイル保存
  or 直接貼り付けでインポートする
- 取り込み時の重複解決・列順自由などのルールは通常の CSV インポートと共通

サンプルプロンプト (テンプレート):
```
あなたは英語教材作成のアシスタントです。
以下のテーマと条件に従って英単語を生成し、指定された CSV 形式のみを
出力してください。前後に説明文を入れないでください。

テーマ: {theme}
件数: {count}
レベル: {level}

CSV 形式 (1 行目はヘッダ固定):
word,meaning,part_of_speech,example,tags,notes
- meaning は日本語、複数訳は ; で区切る
- tags はセル内で ; 区切り
- example は英文 1 文
```

#### 3.6.2 アプリ内 LLM 生成モード (Phase 5)

- ユースケース
  - テーマ (例: "ビジネス英語の動詞") と件数を指定して新規単語を一括生成
  - 既存単語に対して「より自然な和訳の候補」「例文」を後付けで生成
- 抽象化
  - `IVocabularyGenerator` インタフェースを定義し、実装は差し替え可能に
  - 初期実装は Anthropic Claude API (`AnthropicVocabularyGenerator`)
  - 後続で Gemini API 実装 (`GeminiVocabularyGenerator`) も同インタフェースで
    追加できるようにしておく
  - API キーは Windows Credential Manager に保存
- UX
  - 生成結果はそのまま登録せず、確認画面で取捨選択してから単語帳に追加
  - 重複は自動でスキップ候補としてマーク

> Phase 1〜4 までは UI 上「アプリ内 LLM 生成」ボタンは非表示。
> ただし §3.6.1 の「外部 LLM 取り込みモード」は CSV インポートの上に
> 載るだけなので Phase 2 完了時点で利用可能。

## 4. アーキテクチャ

### 4.1 全体構成

```
+------------------------------+
|        View (XAML)           |   WPF Window / UserControl
+------------------------------+
              | DataBinding
+------------------------------+
|        ViewModel             |   CommunityToolkit.Mvvm
+------------------------------+
              | DI
+------------------------------+
|        Services              |   VocabularyService, TestSessionService,
|                              |   CsvService, IVocabularyGenerator
+------------------------------+
              |
+------------------------------+
|     Repository / DbContext   |   EF Core (SQLite)
+------------------------------+
              |
+------------------------------+
|       SQLite (file)          |   %AppData%\VocabApp\vocab.db
+------------------------------+
```

### 4.2 採用パターン
- **MVVM**: 標準。`CommunityToolkit.Mvvm` の `ObservableObject` / `RelayCommand`
  / source generator (`[ObservableProperty]`, `[RelayCommand]`) を活用
- **DI**: `Microsoft.Extensions.DependencyInjection` を `App.xaml.cs` で構築。
  ViewModel もコンテナ経由で解決し、Designer 表示用に design-time VM を別途用意
- **Repository + Service**: ViewModel は Service を呼び、Service が
  Repository (EF Core DbContext のラッパ) を使う
- **Navigation**: 単純なため Frame ベースではなく、メイン画面に
  サイドメニュー + ContentControl で UserControl を切り替える方式

### 4.3 永続化
- **SQLite + EF Core**
  - 単一ユーザ・単一マシン・数千〜数万語規模で十分高速
  - LINQ で習熟度や正答率での絞り込みが書きやすい
  - DB ファイルは `%AppData%\VocabApp\vocab.db`
- CSV はあくまでインポート / エクスポート用の交換フォーマット (master ではない)

### 4.4 主要モデル

```csharp
public class Word
{
    public int Id { get; set; }
    public string Text { get; set; }          // 英単語
    public string Meaning { get; set; }       // 和訳 (;区切り複数訳)
    public PartOfSpeech? PartOfSpeech { get; set; }
    public string? Example { get; set; }
    public string? Notes { get; set; }
    public List<Tag> Tags { get; set; }       // many-to-many
    public int TimesAsked { get; set; }
    public int TimesCorrect { get; set; }
    public DateTime? LastAskedAt { get; set; }
    public int Mastery { get; set; }          // 0..5
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Word> Words { get; set; }
}

public class TestSession
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TestMode Mode { get; set; }
    public List<TestAnswer> Answers { get; set; }
}

public class TestAnswer
{
    public int Id { get; set; }
    public int WordId { get; set; }
    public string UserInput { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
}
```

## 5. UI 構成

### 5.1 メインウィンドウ
- 左: ナビゲーションペイン (`単語一覧 / テスト / インポート・エクスポート / 設定`)
- 右: 選択した画面の UserControl

### 5.2 画面一覧
| 画面 | 役割 |
|------|------|
| 単語一覧 | DataGrid + 検索 / 絞り込み / 並び替え。インライン編集と詳細ダイアログ |
| 単語編集ダイアログ | 単語 1 件の登録・編集 (タグ補完付き) |
| テストセットアップ | 出題範囲・モード・件数の選択 |
| テスト実行 | 1 問ずつ出題。スキップ / ヒント / 終了 |
| テスト結果 | 正答率, 間違えた単語, 「間違えた単語のみで再テスト」ボタン |
| インポート / エクスポート | ファイル選択 + プレビュー + 衝突解決オプション |
| 設定 | テーマ, 判定の厳密度, (将来) API キー設定 |

### 5.3 デザイン方針
- 標準コントロール中心、まず動くものを優先
- ライト / ダークは `ModernWpf` ないし WPF 標準テーマで切替

## 6. 技術スタック

| 区分 | 採用 | 備考 |
|------|------|------|
| ランタイム | .NET 8 (LTS) | Windows 11 専用 (`net8.0-windows`, TargetPlatformMinVersion=10.0.22000) |
| UI | WPF | 既存スキルセットと配布の容易さ |
| MVVM | CommunityToolkit.Mvvm | source generator で boilerplate 削減 |
| DI | Microsoft.Extensions.DependencyInjection | |
| 永続化 | EF Core + SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) | |
| CSV | CsvHelper | RFC 4180 準拠で楽 |
| ロギング | Microsoft.Extensions.Logging + Serilog (file sink) | |
| テスト | xUnit + FluentAssertions + Moq | サービス層中心に単体テスト |
| LLM (将来) | Anthropic.SDK / Google.GenerativeAI など | `IVocabularyGenerator` 抽象越し。Gemini への切替も視野 |
| 配布 | `dotnet publish` フォルダ配布 | MSIX / インストーラは作らない |

## 7. ソリューション構成

```
wpf_single_word_count/
├── docs/
│   └── design.md
├── src/
│   ├── VocabApp.Core/            # ドメインモデル, インタフェース
│   │   ├── Models/
│   │   ├── Services/             # IVocabularyService など
│   │   └── Csv/                  # CSV スキーマ定義
│   ├── VocabApp.Infrastructure/  # EF Core, CsvHelper, LLM 実装
│   │   ├── Persistence/
│   │   ├── Csv/
│   │   └── Llm/
│   └── VocabApp.Wpf/             # WPF アプリ本体 (View + ViewModel)
│       ├── App.xaml
│       ├── Views/
│       ├── ViewModels/
│       └── Resources/
└── tests/
    ├── VocabApp.Core.Tests/
    └── VocabApp.Infrastructure.Tests/
```

ViewModel と Service の境界で単体テストを書きやすくするため、
Core / Infrastructure / Wpf の 3 層構成にする。

## 8. 開発フェーズ (マイルストーン)

| Phase | 内容 | 完了条件 |
|-------|------|----------|
| **0. 基盤** | ソリューション作成, DI / EF Core / マイグレーション, ロガー | 空のメインウィンドウが起動し DB が初期化される |
| **1. 単語 CRUD** | 単語の追加 / 編集 / 削除 / 一覧 + タグ | 手動入力で単語帳を運用できる |
| **2. CSV 入出力 + LLM プロンプト出力** | インポート / エクスポート (v1) + 外部 LLM 用プロンプトテンプレートのコピー | Gemini CLI 等で生成した CSV をそのまま取り込める |
| **3. テスト機能** | 4 モード + 結果集計 + 習熟度更新 | 自分でテストして進捗が記録される |
| **4. 仕上げ** | フィルタ強化, テーマ, ショートカット, エラーハンドリング | 日常運用に耐える品質 |
| **5. アプリ内 LLM 生成** | `IVocabularyGenerator` (Claude 実装) + 生成 → 確認 → 取り込み | テーマ指定で単語が自動追加できる |

各 Phase 完了時に動作確認を行い、必要に応じて設計を見直す。

## 9. 主要な設計判断と代替案

| 判断 | 採用理由 | 却下した代替案 |
|------|----------|-----------------|
| 永続化に SQLite | クエリしやすく将来の SRS 化も見据えやすい | JSON ファイル: 数万語で重くなる / クエリが面倒 |
| MVVM Toolkit | Microsoft 公式・モダン・軽量 | Prism: 機能過多 / MVVM Light: メンテ停止 |
| CsvHelper | 業界標準, 引用符等の罠を回避 | 自作パーサ: バグの温床 |
| 3 層プロジェクト | UI と業務ロジックを分離してテスト容易に | 単一プロジェクト: 早いが後で剥がしづらい |
| LLM はインタフェース先行 | API キー周りを後回しにできる | 最初から実装: スコープが膨らみリリースが遅れる |

## 10. 決定事項 (旧オープン論点)

| # | 論点 | 決定 |
|---|------|------|
| 1 | 対象 OS | **Windows 11 のみ** |
| 2 | 配布形態 | `dotnet publish` のフォルダ配布で十分 |
| 3 | CSV 列名 | §3.3 の v1 でそのまま採用 |
| 4 | 判定の厳密度 | 大文字小文字を区別しない (前後空白無視も維持) |
| 5 | 実装担当 | アプリ本体は Claude が実装。単語リスト内容は Gemini CLI 等にも書かせる可能性あり (§3.6.1) |
| 6 | UI 言語 | 日本語固定 (i18n しない) |

---

> 次のアクション: Phase 0 (ソリューション雛形 + EF Core 初期マイグレーション)
> から着手する。
