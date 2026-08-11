# Oracle

[English](README.md)

緩和・スキルタイミング用の **デューティタイムラインキュー** を表示する Dalamud プラグインです。

ゾーン・ジョブ・シーンに合わせてタイムラインを紐づけます。カウントダウン到達時（または戦闘開始時）に時計が動き、オーバーレイと任意のホットバーハイライトで今後のアクションを示します。

## インストール

Dalamud の **カスタムプラグインリポジトリ**（設定 → Experimental）に次の URL を追加します。

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

プラグインインストーラから **Oracle** をインストールしてください。

## 機能

- **タイムラインエディタ** — 時間オフセットのキュー（アクション／メモ）、ゾーン・ジョブ・SceneId による自動ロード、手動ロードコマンド
- **オーバーレイ** — リスト（今後の行）と Major（スクロールするアイコンレーン）
- **アクションハイライト** — 前後ウィンドウと任意の点滅、任意のホットバーアイコンハイライト
- **FFLogs インポート** — レポートの詠唱を新しいタイムラインへ（API 認証情報は設定から）
- **AutoRecord** — 選択したコンテンツで自分のアクションを記録し、タイムラインへ取り込み
- **i18n** — UI 文字列の英語／日本語（設定で切替可）

## コマンド

| コマンド | 説明 |
| --- | --- |
| `/oracle` | タイムライン設定画面の表示切替 |
| `/oracle config` | プラグイン設定画面の表示切替 |
| `/oracle overlay timeline` | タイムラインオーバーレイの表示切替 |
| `/oracle overlay major` | メジャーオーバーレイの表示切替 |
| `/oracle overlay icon` | アイコンハイライトの表示切替 |
| `/oracle autorecord` | オートレコードの有効切替 |
| `/oracle load <name>` | タイムラインのロード |
| `/oracle unload` | タイムラインのアンロード |
| `/oracle preview start [sec]` | プレビューのカウントダウン開始（省略時 21 秒） |
| `/oracle preview stop` | プレビューの停止 |

## スクリーンショット

![Major オーバーレイとホットバーアイコンハイライト](docs/screenshots/major-hotbar-highlight-730x380.png)

## 開発者向け

1. ビルド: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Dalamud の **dev plugin** パスを `Oracle/bin/Release/` に向ける
3. プラグインインストーラ（dev）で **Oracle** を有効化

共有 UI キットとして [MirageUI](https://github.com/exatrines/MirageUI) を git サブモジュールで同梱しています。

## ライセンス

[AGPL-3.0-or-later](LICENSE)
