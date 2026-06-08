# TagPrint Studio Release Notes

## English

### v1.0.6

#### Main Preview

- Added `Expand`, `Normal`, and `Shrink` controls inside the main window page preview.
- Zoomed page preview now rerenders at a higher internal resolution, improving readability during close inspection.
- Added hand-pan dragging for zoomed page preview in addition to scroll bars.

#### Documentation

- Updated version metadata to v1.0.6.
- Updated README files, release notes, and English/Japanese manuals.

### v1.0.5

#### Backup Management

- Unified CSV backup naming across CSV column add, Tag Finder CSV save, Layout Designer CSV preview save, duplicate cleanup, and CSV merge.
- CSV backups now use timestamped names such as `products.csv.bak-20260608-142530`.
- Added **Backup File Cleanup** under the Settings menu.
  - Previews timestamped CSV backup files.
  - Cleans up old or excess backups by retention days and maximum count per CSV.
  - Moves deleted backups to the Recycle Bin.
- When the startup folder is set or changed, the app can also update the backup cleanup target folder after confirmation.

#### Tag Finder and Queue Workflow

- Added **Open in Layout Designer** from the Tag Finder small preview panel.
- Improved queue creation behavior when **Show true only** is active so queue output applies to the active row instead of every checked row in the visible list.
- Save and queue actions now reset print quantity back to `1`.
- Closing Tag Finder now checks for unused queue CSV files and offers cleanup.

#### CSV Merge Stability

- Fixed a merge preview failure where selecting a valid parent CSV could still throw `The parent CSV could not be loaded` when another CSV in the same folder had a different column count.
- Merge preview now treats merge eligibility separately from cross-file column mismatch status.

#### Documentation

- Updated version metadata to v1.0.5.
- Updated README files, release notes, and English/Japanese manuals.

### v1.0.4

#### Output Stability

- Added progress dialogs for direct print, PDF export, and PNG export.
- Added cancellation support for long-running direct print, PDF export, and PNG export.
- PDF and PNG output now writes to `.partial` files first, then moves completed files into place.
- Canceled PDF/PNG output removes files created by the canceled operation.
- Direct print renders one page at a time and avoids holding all rendered pages in memory.
- Page-level rendering failures now include the page number in the error context.

#### Large Print Warning

- Added a configurable large print warning threshold in the Settings menu.
- The Settings menu label now shows the current threshold or disabled state.

#### CSV and Layout Editing

- Added **Add CSV Column** in the main CSV Fields area.
  - Appends the new column to every loaded CSV file.
  - Writes the column name to the header row.
  - Creates `.bak` backups before overwriting CSV files.
- Added row-specific font-size support for text layout elements.
  - Text elements can use a fixed font size or a selected CSV field.
  - Empty or invalid CSV font-size values fall back to the fixed font size.
  - CSV font-size values are clamped by element min/max limits.

#### Tests and Documentation

- Added service tests for lazy page generation, selected-column sorting, empty printable output, and canceled PNG cleanup.
- Updated version metadata to v1.0.4.
- Updated README files, release notes, and English/Japanese manuals.

### v1.0.3

#### Output and Print Confirmation

- Added an optional print target confirmation dialog before PDF, PNG, and direct print output.
- The dialog shows CSV summaries, row counts, tag counts, page counts, and the active print order.
- Added search to the print target confirmation dialog.
  - Search by order, CSV, row, sort value, or key value.
  - Pressing `Enter` in the search value field runs the search.
  - Matching rows are jumped to and highlighted.
- Added a Settings menu toggle: **Confirm Print Targets Before Output**.

#### Print Order

- Added print order control to the main output area.
- Supported order modes include load order, Item Code order, and selected-column order.

#### Tag Finder

- Added a small tag preview panel to Tag Finder.
- Added a persistent preview on/off checkbox.
- Added print quantity handling.
  - Quantity `1` behaves like normal save.
  - Quantity `2` or more creates a `print_queue_yyyyMMdd_HHmmss.csv` file in the parent CSV folder.
  - Queue rows are generated as `quantity - 1` so the original row plus queue rows equals the requested total.
- Added queue CSV cleanup from Tag Finder.
- Queue CSV files can also be removed from the main CSV list context menu.
- Search focus now starts in the search value field when Tag Finder opens.

#### Refactoring

- Moved print target summary creation into `PrintTargetSummaryService`.
- Moved Tag Finder search and checked-row collection into `TagFinderService`.
- Added dedicated model classes for print target confirmation and Tag Finder matches.

#### Documentation

- Updated version metadata to v1.0.3.
- Rebuilt README files.
- Rebuilt the English and Japanese manuals using the current content and existing screenshots.
- Regenerated release notes with English first and Japanese second.

### v1.0.2

#### Layout Designer

- Improved inline text editing on the Layout Designer canvas.
- The inline edit box now uses a font size that more closely matches the rendered canvas text.
- Changed inline text editing shortcuts:
  - `Enter` applies the edit.
  - `Shift + Enter` inserts a line break.
  - `Esc` cancels the edit.

#### Documentation

- Updated README content for v1.0.2.
- Updated the English and Japanese manuals for v1.0.2.
- Rebuilt Japanese README and manual text to remove mojibake.

### v1.0.1

#### Preview

- Improved the main window's **Single Tag** preview resolution.
- In **Page Preview** mode, **Previous** and **Next** move between pages.
- In **Single Tag** mode, **Previous** and **Next** move between printable tags.

#### Layout Designer

- Added vertical canvas area controls in the bottom-right of the Layout Designer canvas:
  - **Expand**
  - **Normal**
  - **Shrink**
- **Expand** moves the upper splitter upward in 10% steps, up to +50%.
- **Shrink** moves the upper splitter downward in 10% steps, down to -40%.
- **Normal** restores the default splitter position.

### v1.0.0

#### Output Checks

- Undersized barcode elements are now treated as warnings instead of blocking errors.
- PDF and PNG export can continue after acknowledging warnings when no blocking errors are present.
- Blocking preflight errors still stop output when data or layout problems would likely produce invalid results.

---

## Japanese

### v1.0.6

#### メインプレビュー

- メイン画面のページ全体プレビュー内に `拡大`、`標準`、`縮小` ボタンを追加しました。
- ページ全体プレビューの拡大時は内部的に高解像度で再描画するようにし、見やすさを改善しました。
- 拡大したページ全体プレビューは、スクロールバーに加えて手のひらドラッグでも移動できるようにしました。

#### ドキュメント

- アプリバージョンを v1.0.6 に更新しました。
- README、リリースノート、英日マニュアルを更新しました。

### v1.0.5

#### バックアップ管理

- CSV列追加、タグファインダーからの CSV 保存、レイアウトデザイナーの CSV プレビュー保存、重複整理、CSV マージのバックアップ名を統一しました。
- CSV バックアップは `products.csv.bak-20260608-142530` のような日時付き形式になりました。
- 設定メニューに **バックアップファイル整理** を追加しました。
  - 日時付き CSV バックアップを一覧表示します。
  - 保持日数と CSV ごとの最大件数で古いバックアップや余分なバックアップを整理できます。
  - 削除対象はゴミ箱へ移動します。
- 起動時フォルダーを設定・変更した際、確認後にバックアップファイル整理の対象フォルダーも合わせて更新できるようにしました。

#### タグファインダーとキュー運用

- タグファインダーの小型プレビューから **レイアウトデザイナーで開く** を追加しました。
- `trueのみ` 表示中のキュー作成は、表示中の全チェック行ではなく現在対象の行に対して行うようにしました。
- 保存系操作とキュー作成系操作のあと、印刷枚数を `1` に戻すようにしました。
- タグファインダーを閉じる際、未使用のキュー CSV があれば整理確認できるようにしました。

#### CSV マージ安定性

- 同じフォルダーに列数の違う CSV があると、正しい親 CSV を選んでいても `親CSVの読み込みに失敗しました。` になる問題を修正しました。
- CSV マージのプレビューでは、ファイルが読めるかどうかと、親 CSV とヘッダー一致するかどうかを分けて判定するようにしました。

#### ドキュメント

- アプリバージョンを v1.0.5 に更新しました。
- README、リリースノート、英日マニュアルを更新しました。

### v1.0.4

#### 出力安定性

- 直接印刷、PDF 保存、PNG 保存に進捗ダイアログを追加しました。
- 長時間の直接印刷、PDF 保存、PNG 保存でキャンセルできるようにしました。
- PDF/PNG は `.partial` ファイルへ一時保存してから、完成後に最終ファイルへ移動するようにしました。
- PDF/PNG 保存をキャンセルした場合、その操作で作成された出力ファイルを削除します。
- 直接印刷は 1 ページずつ描画し、全ページ画像をメモリに保持しないようにしました。
- ページ単位の描画失敗時に、失敗したページ番号を確認できるようにしました。

#### 大量印刷警告

- 設定メニューで大量印刷警告のページしきい値を変更できるようにしました。
- 設定メニュー上に現在のしきい値、または無効状態を表示するようにしました。

#### CSV とレイアウト編集

- メイン画面の CSV フィールドに **CSV列追加** を追加しました。
  - 読み込み済みすべての CSV に新しい列を追加します。
  - ヘッダー行に列名を書き込みます。
  - CSV 上書き前に `.bak` バックアップを作成します。
- テキスト系レイアウト要素で、行ごとの文字サイズ指定に対応しました。
  - 固定の文字サイズ、または選択した CSV フィールドを文字サイズとして使えます。
  - CSV の文字サイズ値が空、または不正な場合は固定の文字サイズへ戻ります。
  - CSV から読んだ文字サイズは、要素の最小/最大範囲内に収めます。

#### テストとドキュメント

- 遅延ページ生成、選択列ソート、印刷対象なし、キャンセル時 PNG クリーンアップのサービス層テストを追加しました。
- アプリバージョンを v1.0.4 に更新しました。
- README、リリースノート、英日マニュアルを更新しました。

### v1.0.3

#### 出力と印刷対象確認

- PDF 保存、PNG 保存、直接印刷の前に表示できる「印刷対象の確認」ダイアログを追加しました。
- CSV ごとの行数、タグ数、ページ数、現在の印刷順を確認できます。
- 印刷対象の確認ダイアログに検索機能を追加しました。
  - 順番、CSV、行、並び替え値、代表値で検索できます。
  - 検索値欄で `Enter` を押すと検索します。
  - 見つかった行へジャンプし、ハイライト表示します。
- 設定メニューに **出力前に印刷対象を確認** を追加しました。

#### 印刷順

- メイン画面の出力エリアに印刷順の指定を追加しました。
- 読み込み順、Item Code 順、選択列順に対応しました。

#### タグファインダー

- タグファインダーに小型プレビューを追加しました。
- プレビュー表示のオン/オフを保持できるようにしました。
- 印刷枚数の指定に対応しました。
  - 印刷枚数 `1` は通常保存と同じ扱いです。
  - 印刷枚数 `2` 以上では、親 CSV と同じフォルダーに `print_queue_yyyyMMdd_HHmmss.csv` を作成します。
  - キュー CSV の行数は `印刷枚数 - 1` です。親 CSV の元行と合わせて指定枚数になります。
- タグファインダーからキュー CSV を一括削除できるようにしました。
- メイン画面の CSV 一覧でも、キュー CSV の右クリックメニューから削除できます。
- タグファインダー起動時、検索値欄にカーソルが入るようにしました。

#### リファクタリング

- 印刷対象一覧の作成処理を `PrintTargetSummaryService` に分離しました。
- タグファインダーの検索とチェック行取得を `TagFinderService` に分離しました。
- 印刷対象確認とタグファインダー用のモデルを追加しました。

#### ドキュメント

- アプリバージョンを v1.0.3 に更新しました。
- README を更新しました。
- 既存の内容と画像を流用しながら、英語版/日本語版マニュアルを現行機能に合わせて作り直しました。
- リリースノートを英語、次に日本語の順で再生成しました。

### v1.0.2

#### レイアウトデザイナー

- レイアウトデザイナーのキャンバス上のテキスト直接編集を改善しました。
- 直接編集時の入力欄フォントサイズを、キャンバス上に表示されている文字サイズに近づけました。
- 直接編集時のキー操作を変更しました。
  - `Enter` で確定します。
  - `Shift + Enter` で改行します。
  - `Esc` でキャンセルします。

#### ドキュメント

- README を v1.0.2 向けに更新しました。
- 英語版/日本語版マニュアルを v1.0.2 向けに更新しました。
- 日本語 README と日本語マニュアルの文字化けを整理しました。

### v1.0.1

#### プレビュー

- メイン画面の **1枚拡大** プレビューの解像度を改善しました。
- **ページ全体** 表示では **前へ** / **次へ** がページ単位で移動します。
- **1枚拡大** 表示では **前へ** / **次へ** が印刷対象タグ単位で移動します。

#### レイアウトデザイナー

- レイアウトデザイナーのキャンバス右下に縦並びの表示サイズ操作ボタンを追加しました。
  - **拡大**
  - **標準**
  - **縮小**
- **拡大** は上部セパレーターを 10% ずつ上へ移動し、最大 +50% まで拡大できます。
- **縮小** は上部セパレーターを 10% ずつ下へ移動し、最小 -40% まで縮小できます。
- **標準** はセパレーター位置を初期位置に戻します。

### v1.0.0

#### 出力前チェック

- 推奨サイズ未満のバーコード要素を、ブロックエラーではなく警告として扱うようにしました。
- ブロック対象のエラーがない場合、警告確認後に PDF/PNG 保存を継続できます。
- データやレイアウトの問題で無効な出力になる可能性が高い場合は、引き続きブロックエラーとして出力を停止します。
