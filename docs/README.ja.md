# Oracle

[English](../README.md)

![Major オーバーレイとホットバーのアイコンハイライト](screenshots/major-hotbar-highlight-730x380.png)

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

- どのスキルをいつ使うかタイムライン化できます
- FFLogsのレポートからスキルを読み込めます
- 戦闘を記録して、タイムラインに変換できます
- コンテンツ開始時に、対応するタイムラインを自動で読み込みます
- これから使うスキルをリストやスクロールするアイコンで確認できます
- 使うタイミングに合わせてホットバーのスキルを点灯させます

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

[AGPL-3.0-or-later](../LICENSE)
