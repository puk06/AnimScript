namespace net.puk06.AnimScript
{
    /// <summary>
    /// ウィンドウ内に表示する構文チートシート。
    /// Samples フォルダの .animscript も実例として参照。
    /// </summary>
    internal static class SyntaxCheatSheet
    {
        public const string Text =
@"■ パラメータ宣言（ファイルの上の方に）
param Speed : float = 0
param Grounded : bool = true
param GestureLeft : int = 0

■ レイヤー
layer ""Locomotion"" default wd off {
    ...
}
  default … 先頭レイヤーにする
  wd on/off … レイヤー内ステートの WriteDefaults 既定値（writeDefault も可）
  loop on/off … レイヤー内クリップの Loop Time 既定値
  weight 1.0 … レイヤーウェイト

■ ステート
state Idle = ""Idle.anim""           ← クリップを1行で
state Walk = ""Walk.anim"" speed 1.2 cycleOffset 0.25 time MotionTime mirror on footIK on wd off loop on
state Empty                          ← 空ステート（motion 無し）

state Pose {
    clip ""Pose.anim""    （または clip none）
    speed 1.0
    cycleOffset 0.25
    （`co 0.25` と省略可）
    time MotionTime
    mirror on
    footIK on
    wd off
    loop on
    driver localOnly {
        set    パラメータ 値
        add    パラメータ 値
        random パラメータ 最小 最大
        copy   コピー元 -> コピー先
    }
}

■ 遷移
entry -> Idle                        ← 開始地点（デフォルトステート）
Idle -> Walk when Speed > 0.1 dur 0.25 offset 0.1
Idle -> Walk -> Run -> Idle          ← チェーン（先頭に戻ればループ）
Walk -> exit exitTime 0.95           ← Exit へ抜ける
any  -> Idle when GestureLeft == 0   ← AnyState から
any  -> Idle self                    ← self で自分への遷移も許可

■ 条件（when の後）
Param          … true のとき
!Param         … false のとき
Param > 1      … > < == !=（>= <= は無し）
A and B        … 両方 / A or B   … どちらか
(A or B) and C … 括弧もOK（or は複数の遷移に展開。上限16本）

■ ビルド時ループ・変数（くり返し生成）
var i = 0                        ← 変数宣言（参照は $i と書く）
for i in 0..7 { ... }            ← i = 0〜7（両端含む）で中身を展開
for i in 0..10 step 2 { ... }    ← 刻み幅つき
while $i < 8 {                   ← 条件が成り立つ間くり返す
    state Pose$i = ""p$i.anim""
    i = $i + 1                   ← 代入（更新を忘れると無限ループ！）
}
  ・数値の場所では計算できます（+ - * / % と ()）
  ・$i はステート名・クリップパス・条件値・driver の値にも使えます
  ・$$ は $ そのものになります
  ・while の上限は512回です

■ その他
// でコメントが書けます
クリップは「スクリプトからの相対パス」→ 見つからなければ「名前で検索」の順に探します
NDMF ビルド時に自動的に AnimatorController が生成・マージされます";
    }
}
