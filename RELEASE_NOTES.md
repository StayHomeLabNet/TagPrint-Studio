# TagPrint Studio Release Notes

## English

### v1.0.8

#### Output Safety

- Reworked PDF/PNG output replacement so canceled or failed output no longer risks deleting an existing file that had already been overwritten.
- Output files are now written through unique `.partial-*` files before final replacement.
- When an output path already exists, the existing file is temporarily moved to an operation backup and restored if cancellation or failure occurs.
- On cancellation, cleanup removes only files newly created by the canceled operation.
- This improves safety for multi-file PNG export, per-CSV PDF export, and any output mode that can replace existing files.

#### CSV Loading Fidelity

- Blank physical CSV rows are now preserved instead of being silently removed during load.
- Blank rows are written back as blank rows when the CSV is saved.
- Blank rows are not treated as printable data rows.
- Blank rows are ignored by column-count mismatch checks so intentional blank spacing in source CSV files does not create false import errors.

#### CSV Detection

- Delimiter detection now uses up to the first 20 records instead of only the first record.
- Comma, tab, and semicolon candidates are scored by record usage, consistent column count, total delimiter count, and priority.
- Delimiters inside quoted fields are ignored during detection.
- UTF-16 LE BOM and UTF-16 BE BOM CSV files are now detected.
- Encoding detection order is now UTF-8 BOM, UTF-16 LE BOM, UTF-16 BE BOM, valid UTF-8, then Shift_JIS fallback.

#### Settings Backup and Crash Logs

- Settings backup ZIP creation now writes to a temporary ZIP first and replaces the final ZIP only after the archive is created successfully.
- Existing backup ZIP files are no longer deleted before a new backup has been completed.
- Added application-level unhandled exception handling.
- Unexpected errors now show a user-facing error message and write crash logs to the app data `logs` folder.

#### Localization and Platform Target

- Replaced remaining direct `L("Japanese", "English")` dialog strings in cleanup dialogs with shared localization resource keys.
- Updated the application and test projects from `net10.0-windows` to `net8.0-windows` for LTS-friendly build, CI, and distribution.

#### Documentation

- Updated README files for v1.0.8.
- Updated release notes for v1.0.8.
- Rewrote the English and Japanese manuals in the existing format with coverage for current features and safety behavior.

### v1.0.7

#### Tag Finder

- Unified Tag Finder labels, buttons, messages, and column headers with the shared localization resource system.
- Cleaned up remaining garbled Japanese UI text and kept startup checks for older mojibake preset/layout files.
- Improved the **Show True Only** button layout so Japanese text fits more reliably.
- Removed unnecessary suffix text from the Tag Finder preview title.
- Queue creation now saves parent CSV flags when needed so the original row remains the first normal copy.
- Save-to-queue flow now avoids duplicate CSV flag saves.

#### Queue CSV Files

- Queue CSV file creation now avoids overwriting same-second files by adding suffixes such as `-2` and `-3` when needed.
- Documented the queue CSV naming behavior in README and manuals.

#### Settings and Documentation

- Documented app data storage location management, including change, open, and reset operations for the app data root.
- Updated README files, release notes, and English/Japanese manuals for v1.0.7.

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
- Added row-specific font-size support for text layout elements.

#### Tests and Documentation

- Added service tests for lazy page generation, selected-column sorting, empty printable output, and canceled PNG cleanup.
- Updated version metadata to v1.0.4.
- Updated README files, release notes, and English/Japanese manuals.

### v1.0.3

#### Output and Print Confirmation

- Added an optional print target confirmation dialog before PDF, PNG, and direct print output.
- The dialog shows CSV summaries, row counts, tag counts, page counts, and the active print order.
- Added search to the print target confirmation dialog.
- Added a Settings menu toggle: **Confirm Print Targets Before Output**.

#### Print Order

- Added print order control to the main output area.
- Supported order modes include load order, Item Code order, and selected-column order.

#### Tag Finder

- Added a small tag preview panel to Tag Finder.
- Added a persistent preview on/off checkbox.
- Added print quantity handling and queue CSV creation.
- Added queue CSV cleanup from Tag Finder and the main CSV list.
- Search focus now starts in the search value field when Tag Finder opens.

#### Refactoring and Documentation

- Moved print target summary creation into `PrintTargetSummaryService`.
- Moved Tag Finder search and checked-row collection into `TagFinderService`.
- Rebuilt README, manuals, and release notes for the current feature set.

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

- Added vertical canvas area controls in the bottom-right of the Layout Designer canvas.

### v1.0.0

#### Output Checks

- Undersized barcode elements are now treated as warnings instead of blocking errors.
- PDF and PNG export can continue after acknowledging warnings when no blocking errors are present.
- Blocking preflight errors still stop output when data or layout problems would likely produce invalid results.

---

## Japanese

### v1.0.8

#### 出力安全性

- PDF/PNG 出力の置換処理を見直し、キャンセルや失敗時に、上書き済みの既存ファイルまで削除してしまうリスクを減らしました。
- 出力ファイルは一意な `.partial-*` ファイルへ書き込んでから最終ファイルへ置き換えます。
- 出力先に既存ファイルがある場合は一時バックアップへ退避し、キャンセル/失敗時は復元します。
- キャンセル時の cleanup は、その操作で新規作成したファイルだけを削除します。
- 複数 PNG 出力、CSV ごとの PDF 出力、既存ファイルを置き換える出力モードの安全性が向上しました。

#### CSV 読み込み再現性

- 物理的な空行を読み込み時に削除せず保持するようにしました。
- CSV 保存時も空行を空行として書き戻します。
- 空行は印刷対象行として扱いません。
- 空行は列数不一致チェックから除外し、意図的な空行で false error が出ないようにしました。

#### CSV 判定

- 区切り文字判定を先頭 1 レコードだけでなく、先頭最大 20 レコードから行うようにしました。
- カンマ、タブ、セミコロンの候補を、出現レコード数、列数の安定性、総出現数、優先順位で判定します。
- 引用符内の区切り文字は判定対象から除外します。
- UTF-16 LE BOM と UTF-16 BE BOM の CSV を検出できるようにしました。
- 文字コード判定順は UTF-8 BOM、UTF-16 LE BOM、UTF-16 BE BOM、valid UTF-8、Shift_JIS fallback です。

#### 設定バックアップとクラッシュログ

- 設定バックアップ ZIP は一時 ZIP に作成し、成功後にだけ最終 ZIP へ置き換えるようにしました。
- 新しいバックアップが完成する前に既存バックアップ ZIP を削除しないようにしました。
- アプリ全体の未処理例外ハンドラを追加しました。
- 予期しないエラー時にユーザー向けメッセージを表示し、アプリデータの `logs` フォルダーへクラッシュログを保存します。

#### ローカライズとターゲット

- cleanup 系ダイアログに残っていた直接 `L("Japanese", "English")` 方式の文字列を共通ローカライズリソースキーへ移しました。
- アプリ本体とテストプロジェクトを `net10.0-windows` から `net8.0-windows` へ変更し、LTS ベースでのビルド、CI、配布をしやすくしました。

#### ドキュメント

- README を v1.0.8 向けに更新しました。
- リリースノートを v1.0.8 向けに更新しました。
- 既存フォーマットを踏襲しつつ、英日マニュアルを現行機能と安全性に合わせて網羅的に書き直しました。

### v1.0.7

#### タグファインダー

- タグファインダーのラベル、ボタン、メッセージ、列見出しを共通ローカライズリソースへ統一しました。
- 残っていた日本語UI文字列の文字化けを整理し、古い文字化けプリセット/レイアウトを起動時に検出する処理を維持しました。
- **true のみ表示** ボタンの幅と文字サイズを調整し、日本語表示が収まりやすくなりました。
- タグファインダーのプレビュー見出しから不要な付加表示を削除しました。
- キュー作成時に必要に応じて親CSV側のフラグを保存し、親CSVの元行を通常の1枚目として扱えるようにしました。
- 保存からキュー作成へ進む場合、CSVフラグ保存が重複しないようにしました。

#### キューCSV

- 同じ秒にキューCSVを作成した場合でも上書きしないよう、必要に応じて `-2`、`-3` などの連番を付けるようにしました。
- キューCSVのファイル名仕様を README とマニュアルに追記しました。

#### 設定とドキュメント

- アプリデータ保存場所の変更、表示、既定へのリセットについてドキュメントに追記しました。
- README、リリースノート、英日マニュアルを v1.0.7 向けに更新しました。

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
- 起動時フォルダーを設定・変更した際、確認後にバックアップファイル整理の対象フォルダーも合わせて更新できるようにしました。

#### タグファインダーとキュー運用

- タグファインダーの小型プレビューから **レイアウトデザイナーで開く** を追加しました。
- `trueのみ` 表示中のキュー作成は、表示中の全チェック行ではなく現在対象の行に対して行うようにしました。
- 保存系操作とキュー作成系操作のあと、印刷枚数を `1` に戻すようにしました。
- タグファインダーを閉じるとき、未使用のキュー CSV があれば整理確認できるようにしました。

#### CSV マージ安定性

- 同じフォルダー内に列数の違う CSV があっても、有効な親 CSV を選べばプレビューできるようにしました。
- マージ対象判定を、読み込み可否と親 CSV とのヘッダー一致に基づくよう整理しました。

### v1.0.4

#### 出力安定性

- 直接印刷、PDF 保存、PNG 保存に進捗ダイアログを追加しました。
- 大量出力のキャンセルに対応しました。
- PDF/PNG は `.partial` ファイルへ保存してから最終ファイルへ移動するようにしました。
- キャンセル時は、その操作で作成された PDF/PNG を削除します。
- 直接印刷は 1 ページずつ描画し、メモリ使用量を抑えるようにしました。
- ページ単位の描画失敗にページ番号が出るようにしました。

#### 大量印刷警告

- 設定メニューに大量印刷警告のしきい値を追加しました。
- 設定メニュー名に現在のしきい値または無効状態が表示されます。

#### CSV とレイアウト編集

- メイン画面の CSV フィールドに **CSV列追加** を追加しました。
- テキスト要素の文字サイズを CSV 列から行ごとに指定できるようにしました。

### v1.0.3

#### 出力と印刷対象確認

- PDF/PNG/直接印刷の前に、任意で印刷対象確認ダイアログを表示できるようにしました。
- CSV ごとの概要、行数、タグ数、ページ数、印刷順を確認できます。
- 印刷対象確認ダイアログに検索を追加しました。
- 設定メニューに **出力前に印刷対象を確認** を追加しました。

#### 印刷順

- メイン出力エリアに印刷順制御を追加しました。
- 読み込み順、Item Code 順、選択列順に対応しました。

#### タグファインダー

- タグファインダーに小型プレビューを追加しました。
- プレビュー表示のオン/オフを保持できるようにしました。
- 印刷枚数とキュー CSV 作成に対応しました。
- タグファインダーとメイン CSV 一覧からキュー CSV を整理できるようにしました。

### v1.0.2

#### レイアウトデザイナー

- レイアウトデザイナーのキャンバス上での直接テキスト編集を改善しました。
- 編集ボックスの文字サイズを、描画されるキャンバス上の文字サイズに近づけました。
- 直接テキスト編集の操作を `Enter` で確定、`Shift + Enter` で改行、`Esc` でキャンセルに変更しました。

### v1.0.1

#### プレビュー

- メイン画面の **単票プレビュー** の解像度を改善しました。
- **ページプレビュー** では **前へ/次へ** がページ単位で移動します。
- **単票プレビュー** では **前へ/次へ** が印刷対象タグ単位で移動します。

#### レイアウトデザイナー

- レイアウトデザイナーのキャンバス右下に表示領域を調整する縦ボタンを追加しました。

### v1.0.0

#### 出力前チェック

- 小さすぎるバーコード要素をブロックエラーではなく警告として扱うようにしました。
- ブロックエラーがない場合、警告を確認すれば PDF/PNG 出力を続行できます。
- データやレイアウトに重大な問題がある場合は、引き続き出力を停止します。
