# 霧の島の守り手（仮）— Unity 試作

企画書「霧の島の守り手 企画書（仮）」をもとにした、Unity 6.3 LTS 向けのモバイル（Android / iOS・縦画面）試作です。
霧の海に浮かぶ小さな島で町を育て、朝・昼は準備と強化、夜は上陸してくる襲撃者を自分の手で撃退します。

モデルや画像は使わず、島・建物・キャラクターはすべてコードで生成しています。
シーンに置くのは `GameBootstrap` だけで、Play を押せば島ができあがります。

## はじめかた（Windows）

1. Unity Hub で **Unity 6.3 LTS** をインストール（Android Build Support と Visual Studio を含める）
2. Unity Hub で **Universal 3D** テンプレートの新規プロジェクトを作る
3. このリポジトリの `Assets/MistIsland` フォルダを、作ったプロジェクトの `Assets/` の中にコピー
4. Unity のメニュー **MistIsland → セットアップ（設定・シーン・縦画面）** を実行
   - `Assets/MistIsland/Resources/MistIsland/GameConfig.asset`（数値の設定ファイル）を作成
   - `Assets/MistIsland/Scenes/Main.unity` を作成してビルド設定の先頭に登録
   - 画面の向きを縦に固定
5. Play を押す。Game ビューの解像度は縦長（例：1080×1920）にしておくと実機に近くなります

> `GameConfig.autoBootstrap` が有効なので、セットアップ前でも SampleScene のまま Play すれば動きます。
> ただし SampleScene にはポストエフェクトの Volume があり色味が変わるので、Main シーンで確かめるのがおすすめです。

### このリポジトリで Unity プロジェクトごと管理する

このリポジトリのルートは Unity プロジェクトのフォルダ構成（`Assets/` が直下）になっています。
手元の Unity プロジェクトとつなぐには：

1. GitHub Desktop などでこのリポジトリをクローンする
2. 上の手順で作った Unity プロジェクトから `Packages` と `ProjectSettings` フォルダを、クローンしたフォルダにコピーする
3. Unity Hub の「追加 → ディスクから加える」でクローンしたフォルダを開く
4. `Packages` と `ProjectSettings` をコミットする（`Library` などは `.gitignore` で除外済み）

### Android 実機で確かめる

File → Build Profiles で Android に切り替え、Main シーンが先頭にあることを確認してビルドします。
iOS は後から Mac で同じプロジェクトを開いて対応します（コードは共通）。

## 操作

| 操作 | タッチ | キーボード（Windows で試すとき） |
| --- | --- | --- |
| 移動 | 画面左半分をドラッグ（その場にスティックが出る） | WASD / 矢印 |
| 攻撃 | 右下の「攻撃」を押す（押している間くり返す） | Space |
| カメラ回転 | 画面右半分を横にドラッグ | Q / E |
| ズーム | — | マウスホイール |
| 建てる・調べる | 近くに出るボタン | F |
| メニュー | 右上の「メニュー」 | Tab |

## 企画書との対応

| 企画書の項目 | 実装 |
| --- | --- |
| 島・海・霧・プレイヤー移動 | `World/Island.cs`（式で決まるなめらかな地形・木や岩）、`World/Sea.cs`（波）、`World/MistVisuals.cs` + `Resources/MistIsland/MistLit.shader`（時間帯で変わる淡い光と霧）、`Player/PlayerController.cs` |
| 朝3分・昼3分・夜3分（数値は1か所に） | `Core/DayCycle.cs`。長さは `GameConfig` の `morningSeconds / daySeconds / nightSeconds` |
| 夜に敵が上陸、ウェーブで区切らない | `Combat/EnemySpawner.cs`。夜のあいだ途切れなく海から来る。日数で数・強さが増え、3日目・5日目・10日目に新しい敵が加わる |
| 武器ごとに固定の攻撃モーション1つ（スキルなし） | `Player/PlayerCombat.cs`。剣＝近距離の横なぎ、槍＝中距離の突き、弓＝遠距離の射撃 |
| 経験値・レベル・ステータス・ジョブ | `Core/GameManager.cs`、`Player/PlayerStats.cs`。ジョブで武器と能力（体力・速さ）が変わる。剣士 Lv1 / 槍兵 Lv3 / 弓兵 Lv5 |
| 素材ドロップと装備強化 | 敵を倒すと素材とコイン。メニュー「装備」で武器（3種）と防具（Lv2 で開放）を強化 |
| 施設（銀行）と放置収入 | `Town/Building.cs`。施設に収入が貯まり、近づくと受け取れる。畑（Lv5）・鉱山（Lv8・素材を生む）が「銀行以外の収入源」 |
| 防衛装置と放置中の夜の判定 | 見張り塔（Lv2・矢で攻撃）、柵（Lv4・敵を引きつける）。閉じていた間の夜は `Core/OfflineProgress.cs` で「防衛力」と「その夜の敵の強さ」を比べる |
| 島の拡張とアンロック | メニュー「島」から。Lv3/6/9/12/15 で開放、島が大きくなり外側に空き地が増える |
| セーブ | `Core/SaveSystem.cs`（JSON・自動保存・アプリが裏に回ったとき・終了時） |
| 縦画面 UI とカメラ回転 | `UI/Hud.cs`、`UI/TouchArea.cs`、`World/CameraRig.cs` |

### 未決事項の扱い

- **放置中に防衛装置が壊されたときの報酬**：`GameConfig.offlineBreachRule` で切り替えられるようにしました。
  - `KeepUntilBreach`（初期値）：壊されるまでに貯まった分は受け取れる
  - `LoseAll`：その放置期間の報酬はすべてなくなる
- ローグライト要素・住人の役割・収益化・正式タイトルは、企画書どおり保留のまま手を付けていません。

### 夜の流れ（遊んでいるとき）

- 敵は近く（`enemyAggroRadius`）のプレイヤーや建物を狙い、何もなければ町の中心の拠点に向かう
- 建物は壊れても翌朝に直る。拠点が壊されると、施設に貯まっていた収入が奪われる
- プレイヤーが倒れても数秒後に拠点で復活する
- 夜明けになると残った敵は海へ帰る
- 建設・強化・ジョブ変更は朝と昼だけ

## 数値の調整

すべての数値は `GameConfig`（メニュー **MistIsland → 設定ファイル（GameConfig）を選択**）にあります。
時間の長さ、ジョブ・武器・敵・建物の性能とコスト、成長の速さ、放置の判定などをインスペクターで変えられます。
テスト用に、メニュー「島」タブに「次の時間帯へ進める」「最初からやり直す」ボタンがあります。

## 日本語フォント

UI は uGUI の Text を使い、OS の日本語フォント（Windows なら Yu Gothic UI など）を自動で探します。
文字が □ になる端末があれば、Noto Sans JP などの .ttf / .otf を
`Assets/MistIsland/Resources/MistIsland/Fonts/UIFont.ttf` という名前で置いてください（自動でそちらを使います）。

## フォルダ構成

```
Assets/MistIsland/
  Scripts/
    Core/     設定・時間・セーブ・放置計算・ゲーム全体の進行・入力
    World/    島・海・霧・カメラ・形状生成
    Player/   移動・攻撃モーション・能力値
    Combat/   体力・敵・敵の出現・矢
    Town/     空き地の配置・建物・町の管理
    UI/       HUD・タッチ操作・メニュー
  Resources/MistIsland/MistLit.shader   霧と淡い光のシェーダー（URP / Built-in 共通）
  Editor/     セットアップ用メニュー
  Tests/Editor/  放置計算などのテスト（Window → General → Test Runner の EditMode）
```

## 補足・注意

- この試作は Unity エディタ上ではまだ動かしていません。スクリプトは Unity の参照アセンブリに対して型チェックし、
  放置計算・時間帯・成長式のロジックはテストで確かめていますが、見た目や操作感は Unity で開いて確認・調整が必要です。
- シェーダーは自前の光と霧で描くので、影は出ません。URP でも Built-in でも同じ見た目になります。
- 入力は Input System（Unity 6 の初期設定）と旧 Input Manager のどちらでも動くようにしています。
