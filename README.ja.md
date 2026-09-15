# Oracle

[English](README.md)

![Major オーバーレイとホットバーのアイコンハイライト](docs/screenshots/major-hotbar-highlight-730x380.png)

Oracle は、コンテンツ中に「どのスキルを、いつ使うか」を表示する Dalamud プラグインです。

タイムラインは **FFLogs** のレポートから取り込むほか、選んだコンテンツで自分の行動を記録する **AutoRecord** からも作れます。ゾーンとジョブが合うと、カウントダウンまたは戦闘開始で時計が動き、これから使うアクションをオーバーレイに出します。ホットバーのアイコンを光らせることもできます。任意で、ボスの DataID プリセットによる Auto Load も使えます。

## インストール

1. `/xlsettings` を実行し、**試験的機能**タブを開く
2. **カスタムプラグインリポジトリ** に次の URL を追加する:

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. `/xlplugins` を実行し、**Oracle** をインストールする

## 機能

**タイムラインエディタ**  
時刻つきのキュー（アクション、メモ、シンク）を編集できます。ゾーンとジョブで自動ロードでき、任意でボスの DataID プリセットも使えます。コマンドから手動でロードすることもできます。

**オーバーレイ**  
これから使うアクションが表示されるオーバーレイを表示します。タイムライン形式とメジャー形式の２種類が用意されています。

**アクションハイライト**  
アクション発動の前後で、ホットバーをハイライトします。枠で囲うだけではなく、使用タイミングの前後で色を変えたり、点滅させることが可能です。

**FFLogs インポート**  
レポートからアクションを取り込めます。使用するためにはFFLogsAPIの認証情報を設定から登録する必要があります。

**AutoRecord**  
選んだコンテンツで、自分のアクションと敵の詠唱などの情報を記録できます。記録した内容はタイムラインに取り込めます。

**Timer Sync Presets**  
ゾーンごとに、時計を合わせる敵詠唱やプレイヤーのステータスを定義します。タイムラインの作成時に使用することができます。

**Phase Presets**  
ゾーンごとにボスの DataID セットを定義できます。非戦闘時の Auto Load がこれを使います。フェーズ未設定は Any（ゾーンとジョブのみ）です。組み込みは DSR P2 と M12S P2 です。保存した一覧は「デフォルトに戻す」まで組み込みを隠します。旧バージョンの Scene 別ファイルはすべて Any になり、同一ゾーン・ジョブに複数あると自動ロードされません。

**i18n**  
UI は英語と日本語に対応しています。設定から切り替えられます。

## コマンド

| コマンド | 説明 |
| --- | --- |
| `/oracle` | タイムライン設定の表示切替 |
| `/oracle config` | プラグイン設定の表示切替 |
| `/oracle overlay timeline` | タイムラインオーバーレイの表示切替 |
| `/oracle overlay major` | Major オーバーレイの表示切替 |
| `/oracle overlay icon` | アイコンハイライトの表示切替 |
| `/oracle autorecord` | AutoRecord の有効切替 |
| `/oracle load <name>` | タイムラインを読み込む |
| `/oracle unload` | タイムラインを外す |
| `/oracle preview start [sec]` | プレビューのカウントダウンを開始（省略時は 21 秒） |
| `/oracle preview pause` | プレビューの一時停止／再開 |
| `/oracle preview stop` | プレビューを停止 |

## 開発者向け

1. ビルド: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Dalamud の **dev plugin** に `Oracle/bin/Release/` を指定する
3. プラグインインストーラ（dev）で **Oracle** を有効にする

共有 UI キットの [MirageUI](https://github.com/exatrines/MirageUI) を git サブモジュールとして同梱しています。

## ライセンス

[AGPL-3.0-or-later](LICENSE)
