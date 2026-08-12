# Oracle

[English](README.md)

![Major オーバーレイとホットバーアイコンハイライト](docs/screenshots/major-hotbar-highlight-730x380.png)

Oracle は、コンテンツ用タイムラインキューを表示する Dalamud プラグインです。どのスキルを、いつ使うかを把握するためのものです。

タイムライン作成の主経路として **FFLogs からの取り込み**に対応しています。選択したコンテンツで自分のアクションを記録する **AutoRecord** からもタイムラインへ落とせます。ゾーン・ジョブに合わせ、カウントダウンまたは戦闘開始で時計が動き、オーバーレイ（任意でホットバーのアイコンハイライト）に今後のアクションを表示します。

シーンによる Auto Load およびシーン切替キューは **実験的機能**です（テスト中）。

## インストール

1. `/xlsettings` を実行し、**試験的機能**タブを開く
2. **カスタムプラグインリポジトリ** に次の URL を追加する:

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. `/xlplugins` を実行し、**Oracle** をインストールする

## 機能

- **タイムラインエディタ** — 時間オフセットのキュー（アクション／メモ／実験的なシーン切替）、ゾーン・ジョブによる自動ロード（任意の実験的シーンフィルタ）、手動ロードコマンド
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

## 開発者向け

1. ビルド: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Dalamud の **dev plugin** パスを `Oracle/bin/Release/` に向ける
3. プラグインインストーラ（dev）で **Oracle** を有効化

共有 UI キットとして [MirageUI](https://github.com/exatrines/MirageUI) を git サブモジュールで同梱しています。

## ライセンス

[AGPL-3.0-or-later](LICENSE)
