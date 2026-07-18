# VideoCutEditor

VideoCutEditorは、1つの動画から必要な範囲を1つまたは複数切り出すWindows向けアプリです。

本アプリは、Microsoft WinApp CLIとWinUI 3を使用し、OpenAI GPT-5.5およびGPT-5.6との協働で開発しました。

## おすすめポイント

- **再エンコードなしで高速にカット**: Fast copyなら映像と音声を原則そのままコピーし、画質を変えずに素早く切り出せます。
- **映像はそのままで音量だけ正規化**: Fast copyのまま、映像を再エンコードせず音声だけを`-14 LUFS`へ調整できます。
- **再エンコードを細かく設定**: 目標ファイルサイズを指定したエンコードや、映像・音声のフェードイン／フェードアウトに対応しています。
- **HDR動画をSDRへ変換**: HDR動画を一般的なSDR動画へ変換して書き出せます。

## 初回起動

1. 配布ZIPを任意のフォルダーへ展開します。
2. `VideoCutEditor.exe`を起動します。
3. Windowsの警告が表示された場合は、内容を確認して「詳細情報」から「実行」を選びます。現在のportable版はコード署名されていません。
4. ffmpegとffprobeがPATH上にあれば自動検出されます。
5. 検出されない場合は歯車ボタンから設定を開き、両方のEXEが入ったフォルダーを選択します。片方がない場合は個別のファイル指定欄が表示されます。
6. 設定画面で出力フォルダーを選び、「保存」を押します。

ffmpegとffprobeは本アプリに同梱されていません。未導入の場合はwingetなどでffmpegをインストールしてください。

## 基本操作

1. 「開く」または画面へのドラッグ＆ドロップで動画を読み込みます。
2. 動画またはタイムラインで再生位置を移動します。左右キーでは1フレームずつ移動できます。
3. `[`ボタンまたは`[`キーで開始位置、`]`ボタンまたは`]`キーで終了位置を設定します。開始・終了欄へ時刻を直接入力することもできます。
4. 1つだけ切り出す場合は、必要に応じて出力ファイル名を変更します。
5. 書き出し方法を選び、「書き出し」を押します。複数範囲を切り出す場合は、次の手順で先にクリップを登録します。

## 複数クリップをまとめて書き出す

1. 開始・終了位置を設定し、その右側の「クリップタイトル」へ名前を入力して「追加」を押します。
2. タイトルが空欄の場合は、`クリップ_1`、`クリップ_2`のような名前が自動で付きます。
3. 別の範囲へ移動して開始・終了位置を設定し、必要な数だけ追加を繰り返します。
4. 最初の追加時に開く「書き出しリスト」で登録内容を確認します。鉛筆ボタンで編集、削除ボタンで登録解除ができます。
5. 鉛筆ボタンを押すと、そのタイトルと開始・終了位置がメイン画面へ戻り、「追加」ボタンが「上書き」に変わります。範囲を変更して「上書き」を押すと、確認後に同じ項目を更新します。同名の項目は増えません。
6. メイン画面の「書き出し」を押すと、登録順に各項目を`タイトル.mp4`として出力します。

## Fast copy（エンコードなし）

- 映像と音声を原則そのままコピーするため、高速で画質も変わりません。
- カット位置はキーフレームの都合で、指定時刻から少しずれる場合があります。
- HDR動画はHDRのまま出力されます。
- 「音量を正規化」を有効にした場合、映像はコピーされますが音声は再エンコードされます。

通常の切り抜きは、まずFast copyをおすすめします。

## Re-encode（エンコードあり）

- カット位置をより正確にしたい場合や、コーデック・ビットレート・品質を変更したい場合に使います。
- H.264、H.265、AV1と、利用可能なSoftware/NVEncエンコーダーを選べます。
- レート制御は映像ビットレート、目標サイズ、Qualityから選べます。
- フェードを使う場合はRe-encodeが必要です。
- HDR動画では「HDRをSDRに変換」が表示され、既定で有効になります。HDRのまま出力したい場合はチェックを外します。
- 「音量を正規化」を有効にすると、音量を`-14 LUFS`へ調整して音声を再エンコードします。

書き出しの進行状況やログは、情報ボタンから確認できます。完了後は出力予定欄のフォルダーボタンから保存先を開けます。

## 対応範囲

- 1つの動画から複数範囲を登録し、それぞれ独立したMP4として一度に書き出せます。
- 複数範囲を1本へ結合する機能、複数動画の同時編集、複数トラック編集には対応していません。

## 開発者向け情報

### Start Here

- `AGENTS.md` contains always-on repository instructions for Codex.
- `docs/product-spec.md` is the product behavior source of truth.
- `docs/technical-design.md` is the architecture and ffmpeg export source of truth.
- `docs/codex-workflow.md` describes how Codex should work in this repo.
- `docs/implementation-kickoff.md` is the handoff checklist for the implementation session.
- `docs/project-file-notes.md` records VS Code/C# Dev Kit project-file decisions, including the design-time reference fallback for `VideoCutEditor.Core` diagnostics.

The repo-local skills in `.agents/skills` include the VideoCutEditor workflow skill and Microsoft WinUI skills from `microsoft/win-dev-skills`.

### Current Projects

- `src/VideoCutEditor` - WinUI 3 desktop app shell.
- `src/VideoCutEditor.Core` - testable settings, models, and service contracts.
- `tests/VideoCutEditor.Tests` - xUnit coverage for deterministic core behavior.

### Verify

```powershell
dotnet test VideoCutEditor.slnx
dotnet build src/VideoCutEditor/VideoCutEditor.csproj -p:Platform=x64
```

### Debug In VS Code

Open the repository root, then choose `VideoCutEditor: Debug x64` from Run and Debug before pressing F5. VS Code is pinned to `VideoCutEditor.slnx` through `.vscode/settings.json` so the C# language service opens the repo-standard solution. The WinUI app project also exposes a design-time-only `VideoCutEditor.Core` reference to keep Roslyn LSP diagnostics aligned with the normal project reference. This configuration runs the `build VideoCutEditor x64` task first, builds with `WindowsPackageType=None`, and launches the x64 Debug output directly so VS Code can attach breakpoints. The C# Dev Kit generated profile is kept as a fallback, but it can choose a mismatched platform folder on some machines.

If the build reports that `VideoCutEditor.exe` is locked, close the previous debugged app instance and press F5 again.

If VS Code still shows stale `VideoCutEditor.Core` or generated MVVM warnings after pulling changes, run `Developer: Reload Window` or restart the C# language server.

### Publish

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Portable.ps1 -Platform x64 -Configuration Release
powershell -ExecutionPolicy Bypass -File scripts\Publish-AllPortable.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File scripts\New-PortableRelease.ps1 -Version 0.3.0 -Platform x64
powershell -ExecutionPolicy Bypass -File scripts\New-InstallerRelease.ps1 -Version 0.3.0 -Platform x64
```

The portable publish output is written under `src\VideoCutEditor\bin\Release\...\publish`.
The WinUI app is built self-contained so packaged debug launches and portable output do not depend on a machine-wide .NET runtime probe.
`Publish-Portable.ps1` also runs `scripts\Test-PortablePublish.ps1` to verify that the output contains `VideoCutEditor.exe`, does not include sidecar runtime files, and does not bundle ffmpeg or ffprobe.
`Publish-AllPortable.ps1` runs the same publish and validation flow for x64, x86, and arm64.
`New-PortableRelease.ps1` creates a versioned ZIP plus a SHA-256 checksum under `artifacts\releases`. The ZIP contains the single-file EXE, Japanese usage guidance, and official license notices; ffmpeg and ffprobe remain external.
`New-InstallerRelease.ps1` creates an NSIS installer and checksum. The installer requires no administrator privileges and installs for the current user under `%LocalAppData%\Programs\VideoCutEditor`.

### License

VideoCutEditor source is available under the [MIT License](LICENSE). Official dependency license and notice files are retained under [`third-party`](third-party).
