# アーキテクチャ

このドキュメントは **AnimScript の内部構造と、開発者が保守・拡張する際のガイド** です。一般ユーザー向けの使い方は `README.md` を見てください。

---

## 処理の流れ

`.animscript` から `.controller` ができるまで、以下のパイプラインで処理されます。

```
.animscript
    │
    ▼
┌─────────────┐
│ Lexer       │  字句解析（コメント除去、トークン列に変換）
└──────┬──────┘
       │ List<Token>
       ▼
┌─────────────┐
│ Parser      │  構文解析（トークン列を AST に変換）
└──────┬──────┘
       │ ScriptAst（ループ・変数を含む「生」の構文木）
       ▼
┌─────────────┐
│ AstExpander │  for / while / var を展開して平坦化
└──────┬──────┘
       │ ExpandedAst（レイヤー・ステート・遷移の並び）
       ▼
┌─────────────┐
│ ScriptValidator│ 意味チェック（未定義パラメータ、未定義ステート等）
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ ClipResolver│  クリップ名・相対パスを実際のアセットに解決
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Builders    │  AnimatorController・Layer・State・Transition・Driver を組み立て
└──────┬──────┘
       │
       ▼
.controller
```

`ControllerImporter`（AnimatorController → animscript）はこの流れの**逆**を担当しますが、for/while などの高レベル構造は復元しません。1対1でテキスト化するだけです。

---

## ディレクトリ構成

```
Packages/net.puk06.animscript/
├── package.json                         … VPM パッケージ定義
├── README.md                            … ユーザー向けドキュメント
├── ARCHITECTURE.md                      … このファイル
├── CHANGELOG.md                         … バージョン履歴
├── Samples/                             … 動作サンプル（01〜04）
└── Editor/
    ├── net.puk06.animscript.asmdef      … エディターアセンブリ定義
    ├── AnimScriptBuilderWindow.cs       … メインウィンドウ UI
    ├── SyntaxCheatSheet.cs              … ウィンドウ内チートシート
    ├── ImportMenuItem.cs                … Project ウィンドウの右クリックメニュー
    │
    ├── Language/                        … 言語フロントエンド（Unity 非依存）
    │   ├── Lexer.cs                     … 字句解析
    │   ├── Parser.cs                    … 構文解析
    │   └── Ast/                         … AST ノード
    │       ├── ScriptAst.cs
    │       ├── LayerAst.cs
    │       ├── StateAst.cs
    │       ├── TransitionAst.cs
    │       ├── ParameterAst.cs
    │       ├── DriverAst.cs
    │       ├── ForLoopAst.cs
    │       ├── WhileLoopAst.cs
    │       ├── VarAssignAst.cs
    │       ├── ConditionExprAst.cs
    │       └── ExpressionAst.cs
    │
    ├── Compiler/                        … コンパイル・生成
    │   ├── AnimatorCompiler.cs          … 統括。BuildInto(source, VAC) を NDMF プラグインから呼ぶ
    │   ├── AstExpander.cs               … AST 展開（for/while/var）
    │   ├── ScriptValidator.cs           … 意味チェック
    │   ├── ClipResolver.cs              … AnimationClip の解決
    │   ├── DiagnosticBag.cs             … 診断メッセージの収集
    │   ├── Diagnostic.cs                … エラー/警告の行番号付きメッセージ
    │   ├── DiagnosticBag.cs             … Diagnostic の収集
    │   └── Builder/                     … Unity オブジェクト生成
    │       ├── （Builder 群）            … VirtualLayer/VirtualState/VirtualTransition 等を構築
    │       ├── LayerBuilder.cs
    │       ├── StateBuilder.cs
    │       ├── TransitionBuilder.cs
    │       ├── ParameterDriverBuilder.cs
    │       └── StateMachineLayoutBuilder.cs
    │
    ├── Importer/                        … 逆変換（Controller → animscript）
    │   ├── ControllerImporter.cs        … インポートの統括
    │   ├── DriverExporter.cs            … VRC Parameter Driver → driver ブロック
    │   ├── NameSanitizer.cs             … Unity 名 → animscript 識別子
    │   ├── ScriptTextWriter.cs          … テキスト組み立て
    │   └── ImportMenuItem.cs            … 右クリックメニュー
    │
    └── Util/
        ├── AnimScriptPaths.cs           … 出力パス・相対パス変換
        └── GuiHelper.cs                 … ウィンドウ用の小物
```

### 各層の責務

| 層 | 役割 | Unity 依存 |
|---|---|---|
| `Language` | 文字列 → AST | 無し（UnityEngine / UnityEditor を参照しない） |
| `Compiler` | AST → AnimatorController | 有り（UnityEditor.Animations） |
| `Importer` | AnimatorController → 文字列 | 有り（UnityEditor.Animations） |
| `Editor` ルート | UI / メニュー | 有り（UnityEditor, UnityEngine） |

`Language` 層が Unity 非依存なのは意図的です。`csc` などで単体コンパイルし、CLI から直接テストできるようにしています。

---

## 主要な型

### Token

`Language/Lexer.cs` で生成される最小単位。

| 種類 | 例 |
|---|---|
| `Number` | `0`, `1.5`, `-3` |
| `String` | `"Idle.anim"` |
| `Identifier` | `state`, `Speed`, `GestureLeft` |
| `Keyword` | `param`, `layer`, `for`, `when`, `and`, `or` 等 |
| `Symbol` | `{`, `}`, `->`, `==`, `=` 等 |

### AST ノード

| ノード | 保持する情報 |
|---|---|
| `ScriptAst` | ファイル全体。パラメータ、レイヤー、ループ、変数宣言のリスト |
| `LayerAst` | レイヤー名、オプション（`default`, `wd`, `loop`, `weight`）、ステート/遷移のリスト |
| `StateAst` | ステート名、クリップパス、オプション（`speed`, `wd`, `loop`）、driver |
| `TransitionAst` | 遷移元/遷移先（チェーン対応）、条件、exitTime/dur/self |
| `ParameterAst` | `param` 宣言（名前、型、初期値） |
| `DriverAst` | driver ブロック（`localOnly` フラグ、アクションのリスト） |
| `ForLoopAst` | `for i in 0..7 step 2 { ... }` |
| `WhileLoopAst` | `while $i < $count { ... }` |
| `VarAssignAst` | `var` / 代入（ビルド時変数） |
| `ConditionExprAst` | 遷移条件の式。`and`/`or`/`!`/比較/括弧 |
| `ExpressionAst` | 数値式。`+ - * / %` と変数参照 `$x` |

---

## 設計上の決め事

### 1. `AstExpander` でループを先に展開する

Parser は `for` / `while` / `var` をそのまま AST にします。Validator や Builder は「ループが無い世界」を前提に作るため、必ず `AstExpander.Expand(ScriptAst)` を通します。

メリット：
- Validator, ClipResolver, 各 Builder がシンプルになる
- ユーザーが見る行番号は展開**前**の位置を維持できる（Token の位置情報を引き継ぐため）

### 2. `or` は分配・展開（DNF）で対応

Unity の `AnimatorCondition` は 1 遷移あたり AND リストしか持てないため、`or` は複数遷移に展開します。

```txt
A -> B when X or Y           → A -> B [X], A -> B [Y]
A -> B when (X or Y) and Z   → A -> B [X and Z], A -> B [Y and Z]
```

`AstExpander` の段階で「条件式を DNF（OR of ANDs）」に正規化し、AND 単位で `TransitionAst` を複製します。展開後の遷移名は `[1]`, `[2]` のように番号を付けて区別します。

### 3. 生成物は「中身を消して再利用」

NDMF 化により、AnimatorController ファイルへの直接書き出しは行わず、<br>
`VirtualAnimatorController` を編集して NDMF のコミット処理に任せます。

メリット：
- GUID が変わらないため、アバターの Playable Layer 参照が切れない
- `.meta` も維持される

### 4. エラーは可能な限り続行して収集する

`DiagnosticBag` に複数のエラー/警告をため、最後にまとめて返します。ユーザーは 1 回のビルドでできるだけ多くの問題を直せます。

ただし、構文エラーが深刻な場合は Parser が例外を投げて早期終了することもあります。

### 5. インポーターは「可逆でなくてもよい」

`ControllerImporter` の目標は「元のコントローラの 1:1 テキスト表現」を作ることです。for ループや変数に戻す再構成は行いません。これにより、

- コードが単純になる
- Unity の情報損失（BlendTree, Trigger 型, サブステートマシン等）を警告で明示できる
- 再度ビルドしても、元のコントローラの構造（ステート配置・遷移網）がそのまま復元できる

---

## 構文を追加するレシピ

例として、新しいレイヤーオプション `xyz 数値` を追加するときの流れです。

### 1. 字句・構文を決める

```txt
layer "Locomotion" xyz 1.5 { }
```

### 2. Parser で読み取る

`Language/Parser.cs` のレイヤーオプション解析ループに分岐を追加し、`LayerAst.Options` に値を入れます。

### 3. AST にフィールドを追加

`Language/Ast/LayerAst.cs` に `float? Xyz { get; set; }` のようなプロパティを追加します。

### 4. AstExpander でコピー

`Compiler/AstExpander.cs` の `ExpandLayer` などで、オプションをコピーする処理を追加します。**これを忘れると for ループ内でオプションが消えます**。

### 5. ScriptValidator でチェック

`Compiler/ScriptValidator.cs` で値の範囲チェックなどを行います。

### 6. Builder で Unity に反映

`Compiler/Builder/LayerBuilder.cs` で `AnimatorControllerLayer` の該当フィールドを設定します。

### 7. ドキュメント・チートシートを更新

- `Editor/SyntaxCheatSheet.cs`
- `README.md`
- 必要なら `Samples/05_xxx.animscript` を追加

### 8. テスト

- `Language` 層の変更なら、簡易 CLI ハーネスを作って `csc` でコンパイル＆実行可能
- `Compiler` 層の変更なら Unity エディターでビルドし、生成された `.controller` を検証

---

## テストの方針

### Language 層の単体テスト

`Language` フォルダは Unity に依存しないので、以下のようにしてコマンドラインからテストできます。

```powershell
csc /target:library /out:AnimScript.Language.dll `
    /recurse:Packages\net.puk06.animscript\Editor\Language\*.cs
```

あとは簡易なテスト用 `Main` を書いて、Lexer/Parser/AstExpander の入出力を assert します。

### Compiler / Importer 層のテスト

UnityEditor の API を使うため、Unity 上でテストします。

- サンプル `.animscript` をビルドし、生成物のパラメータ数・レイヤー数・遷移数を目視/ログで確認
- 生成した `.controller` を `ControllerImporter` で再び `.animscript` に戻し、主要な構造が再現されることを確認（完全一致は目指さない）

---

## 既知の制限と設計的な意思決定

| 項目 | 理由 |
|---|---|
| ブレンドツリー未対応 | テキスト表現が複雑になり、本ツールの「シンプルに書く」目的とずれる |
| サブステートマシン未対応 | 同上。現状では警告を出してスキップ |
| `Trigger` 型パラメータ未対応 | 遷移条件で扱えないため、bool として読み替え |
| `>=` / `<=` 条件未対応 | Unity の `AnimatorConditionMode` に存在しないため |
| 遷移時間の割合指定未対応 | animscript では秒指定のみ。インポート時に警告 |

---

## 追加したいときの連絡先

このプロジェクトは **ぷこ（puk06）** が個人で開発・保守しています。
Issue や Pull Request を送る前に、変更したい内容が `Language` 層か `Compiler` 層かを把握しておくとスムーズです。
