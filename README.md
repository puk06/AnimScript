# Animator Builder（animscript）

テキストファイル（**.animscript**）を書くだけで、Unity の **AnimatorController を自動生成**するエディターツールです。
VRChat アバター（Avatar 3.0）での利用を想定しており、**VRC Avatar Parameter Driver** にも対応しています。

```
param Speed : float = 0

layer "Locomotion" default wd on {
    state Idle = "Idle.anim"
    state Walk = "Walk.anim"

    entry -> Idle

    Idle -> Walk when Speed > 0.1 dur 0.2
    Walk -> Idle when Speed < 0.1 dur 0.2
}
```

これをビルドすると、パラメータ・レイヤー・ステート・遷移がすべて設定された `.controller` が出力されます。

---

## 特徴

- テキストで Animator を定義 → **差分が見やすく、Git 管理に強い**
- **for / while ループ**でジェスチャー分岐などをまとめて生成
- **VRC Parameter Driver**（set / add / random / copy）対応
- 生成物はスクリプトと同じ場所の `build/` に出力。再ビルドしても **GUID を維持**（アバターに設定済みの参照が切れません）
- エラーは**行番号付き**でウィンドウとコンソールに表示

## 動作環境

- Unity 2022.3 以降
- VRChat SDK3（Avatar 3.0）… `driver` ブロックを使う場合のみ必須。無い環境では driver は警告付きでスキップされます

## 使い方

1. Unity メニューの **Tools > ぷこのツール > Animator Builder** を開く
2. `.animscript` ファイルをウィンドウに **ドラッグ＆ドロップ**（または ObjectField から選択）
3. **ビルド** ボタンを押す
4. スクリプトと同じフォルダの **`build/スクリプト名.controller`** が生成されます

- ウィンドウ下部の「構文チートシート」に簡易リファレンスがあります
- `Samples/` フォルダに実例が 4 本入っています（これらもそのままドキュメントです）
- 生成物は「生成されたコントローラを選択」ボタンで即座に選択できます

---

# 構文一覧

## コメント

```txt
// 行コメントが書けます
```

## パラメータ宣言（param）

ファイルの上の方に書きます。Animator Controller の Parameters に追加されます。

```txt
param Speed       : float = 0
param GestureLeft : int   = 0
param Grounded    : bool  = true
```

- 型は `float` / `int` / `bool`
- 初期値は省略可（省略時は 0 / false）
- 条件（`when`）で使うパラメータは **必ず宣言が必要** です（VRChat 標準の `GestureLeft` なども）

## レイヤー（layer）

```txt
layer "Locomotion" default wd on loop off weight 1.0 {
    ...
}
```

| オプション | 意味 | 既定値 |
|---|---|---|
| `default` | このレイヤーをコントローラの先頭にする | 最初のレイヤー |
| `wd on/off` | レイヤー内ステートの Write Defaults 既定値 | `on`（Unity 標準） |
| `loop on/off` | レイヤー内クリップの Loop Time 既定値 ※ | 変更しない |
| `weight 数値` | レイヤーウェイト | `1.0` |

複数レイヤーも書けます。`default` は1つだけ。

> ※ `loop` 指定は **クリップのアセット自体** の Loop Time を書き換えます（ビルド時に通知が出ます）。

## ステート（state）

### 1行書き

```txt
state Idle = "Idle.anim"                  // クリップ指定
state Walk = "Walk.anim" speed 1.2 wd off // オプション付き
state Empty                               // 空ステート（motion 無し）
```

### ブロック書き

```txt
state Pose {
    clip "Pose.anim"    // または clip none （明示的に空）
    speed 1.0
    wd off
    loop on
    driver { ... }
}
```

| オプション | 意味 |
|---|---|
| `speed 数値` | ステートの再生速度 |
| `wd on/off` | Write Defaults（レイヤー指定より優先） |
| `loop on/off` | このクリップの Loop Time ※レイヤー指定より優先 |
| `driver { }` | VRC Parameter Driver（下記） |

`{` は次の行に書いても OK（Allman スタイル）です。

## VRC Parameter Driver（driver）

ステートに入ったときにパラメータを書き換えます。
`VRCAvatarParameterDriver` としてステートにアタッチされます。

```txt
state ThumbsUp {
    clip "ThumbsUp.anim"
    driver localOnly {
        set    MyToggle 1        // Set（bool には true/false も可）
        add    MyFloat 0.5       // Add
        random MyInt 0 10        // Random（最小 最大）
        copy   MyFloat -> MyInt  // Copy（コピー元 -> コピー先）
    }
}
```

- `localOnly` を付けるとドライバーの Local Only が ON になります
- driver の対象パラメータが `param` 宣言されていない場合は **警告** が出ます（VRCExpressionParameters 側で管理する運用もあるため、エラーではなく警告です）

## 遷移（->）

```txt
entry -> Idle                              // 開始地点（デフォルトステート）

Idle  -> Walk when Speed > 0.1 dur 0.25    // 条件付き遷移
Idle  -> Walk -> Run -> Idle               // チェーン（A→B→C と順に作られる）
Idle  -> Walk -> Run -> Idle exitTime 0.95 // ↑先頭に戻れば「ループ」

Walk  -> exit exitTime 0.95 dur 0.1        // Exit へ抜ける
any   -> Idle when GestureLeft == 0        // AnyState から
any   -> Idle self                         // self … 自分自身への遷移も許可
```

| オプション | 意味 |
|---|---|
| `when 条件` | 遷移条件（`and` で複数指定可） |
| `exitTime 数値` | Exit Time。**書くと HasExitTime が ON**、書かなければ OFF |
| `dur 数値` | 遷移時間（秒） |
| `self` | AnyState 遷移で canTransitionToSelf を ON（`any` からのみ） |

### 条件の書き方

```txt
when Grounded                         // bool が true のとき
when !Grounded                        // bool が false のとき
when Speed > 0.1                      // >  <  ==  != が使えます（>= <= は無し）
when Speed > 0.1 and Grounded         // and … 両方満たす
when GestureLeft == 2 or GestureLeft == 3   // or … どちらか満たす
when (A or B) and (C or D)            // 括弧でのグルーピングも可
```

- `and` は `or` より優先されます（`A or B and C` = `A or (B and C)`）
- **`or` は内部で複数の遷移に展開されます**（Unity の遷移条件は AND のリストしか持てないため）
  - 例: `A -> B when X or Y` → `A -> B [X]` と `A -> B [Y]` の2本の遷移
  - 括弧を組み合わせた場合は分配されます: `(A or B) and (C or D)` → 4本の遷移
  - or で分かれた遷移には `[1]` `[2]` のような番号が付きます
- 1行から生成される遷移は **16本まで** です
- `!(A or B)` のような括弧ごとの否定は使えません（`!A and !B` のように分解してください）
- `>=` / `<=` は **使えません**（Unity の AnimatorCondition に無いため）。`>` `<` で書き直してください

## ビルド時変数（var / 代入）

コントローラには出力されない、ビルド専用の変数です。

```txt
var count = 4        // 宣言（$ は付けない）
count = $count + 1   // 代入（参照するときは $count と書く）
```

数値を書ける場所ではどこでも計算式が使えます（`+` `-` `*` `/` `%` と `()`）。

```txt
param Max : int = $count * 2
state Pose = "pose.anim" speed 1.0 + 0.5
```

## for ループ

範囲を指定して中身をくり返し展開します（**両端を含みます**）。

```txt
for i in 0..7 {
    state Pose$i = "Gestures/Left_$i.anim"
    Idle   -> Pose$i when GestureLeft == $i dur 0.1
    Pose$i -> Idle   when GestureLeft != $i dur 0.1
}
```

- `step` で刻み幅を変えられます：`for i in 0..10 step 2 { }`（負の step も可）
- ループ変数は `$i` のように `$` を付けて参照します
- 上限は **512 回** です
- ネスト（入れ子）もできます

## while ループ

条件が成り立つ間、中身をくり返し展開します。

```txt
var i = 0
while $i < $count {
    state Item$i = "Menu/Item$i.anim"
    i = $i + 1        // カウンタの更新を忘れずに！
}
```

- while の条件では `==` `!=` `<` `>` `<=` `>=` がすべて使えます（自分たちで評価するため）
- 上限は **512 回**。超えると「無限ループの可能性」エラーになります

## 変数の埋め込み（$変数）

`$変数名` はステート名・クリップパス・パラメータ名など、文字列や名前の中にも書けます。

```txt
for i in 0..3 {
    state Pose$i = "clips/pose_$i.anim"   // Pose0, Pose1, ... ができる
}
```

- `$$` と書くと `$` そのものになります（例: `"price_$$.anim"` → `price_$.anim`）
- ループは **ファイル直下・layer 内・driver 内** のどこでも使えます

---

# クリップの探し方

`state` に書いた文字列は、次の優先順位で探します。

1. **スクリプトからの相対パス**：`state Idle = "Animations/Idle.anim"`（`.anim` は省略可）
2. **Assets からのパス**：`state Idle = "Assets/Anims/Idle.anim"`
3. **クリップ名で検索**：`state Idle = "Idle"`（`/` を含まない場合。fbx 内のクリップも対象）

同じ名前のクリップが複数見つかった場合は、候補一覧付きのエラーになります。パスで指定してください。

# 生成物の仕様

- 出力先：スクリプトと同じフォルダの `build/スクリプト名.controller`
- 再ビルド時は **既存ファイルの中身を消して作り直す** ため、GUID が維持されます
  （アバターの Playable Layer に設定済みでも参照は切れません）
- 生成物は手で編集しないでください（再ビルドで消えます）
- ステートは定義順に2列で自動配置されます

# エラーと警告

ビルドに失敗すると、ウィンドウ下部と Unity コンソールに**行番号付き**で表示されます。

主なエラー：

| メッセージ | 原因 |
|---|---|
| パラメータ「X」は宣言されていません | `when` で使うパラメータは `param` 宣言が必要 |
| ステート「X」はレイヤー「Y」に定義されていません | 遷移先のスペルミス、または `state` 宣言忘れ |
| 「>=」は遷移条件には使えません | `>` `<` で書き直してください |
| while が 512 回を超えました | カウンタ変数の更新忘れ（無限ループ） |
| 変数「X」は宣言されていません | `var` で宣言してから `$x` で参照してください |

主な警告（ビルドは成功します）：

- 「どこからも遷移できません」… 到達不能なステートがあります
- 「entry -> が無いため…」… 最初のステートがデフォルトになります
- 「driver の対象パラメータが param 宣言されていません」… 意図的なら無視して OK

# 注意点・制限

- キーワード（`state` `layer` `param` `for` `while` `entry` `exit` `any` `when` など）と同じ名前のステートは作れません
- ブレンドツリー・サブステートマシンは未対応です
- `loop on/off` はクリップのアセット自体を変更します（他の Animator とも共有される点に注意）

# ファイル構成（開発者向け）

```
AnimatorBuildor/
├── Editor/
│   ├── AnimatorBuilderWindow.cs   … ウィンドウUI
│   ├── SyntaxCheatSheet.cs        … ウィンドウ内チートシート
│   ├── Language/                  … 言語フロントエンド（Unity 非依存）
│   │   ├── Lexer.cs               … 字句解析
│   │   ├── Parser.cs              … 構文解析
│   │   └── Ast/                   … AST ノード
│   ├── Compiler/
│   │   ├── AstExpander.cs         … for/while/var の展開
│   │   ├── ScriptValidator.cs     … 意味チェック
│   │   ├── AnimatorCompiler.cs    … 生成の統括
│   │   └── （各種 Builder）       … State/Transition/Driver など
│   └── Util/AnimScriptPaths.cs    … 出力パス周り
└── Samples/                       … 実例サンプル（01〜04）
```

処理の流れ：**Lexer → Parser → AstExpander → ScriptValidator → ClipResolver → 生成** の順です。
