Unicode true
RequestExecutionLevel user
SetCompressor /SOLID zlib

!ifndef APP_VERSION
  !error "APP_VERSION must be provided by New-InstallerRelease.ps1."
!endif
!ifndef SOURCE_DIR
  !error "SOURCE_DIR must be provided by New-InstallerRelease.ps1."
!endif
!ifndef OUTPUT_DIR
  !error "OUTPUT_DIR must be provided by New-InstallerRelease.ps1."
!endif
!ifndef OUTPUT_BASE_NAME
  !error "OUTPUT_BASE_NAME must be provided by New-InstallerRelease.ps1."
!endif

!include "MUI2.nsh"

Name "VideoCutEditor"
OutFile "${OUTPUT_DIR}\${OUTPUT_BASE_NAME}.exe"
InstallDir "$LOCALAPPDATA\Programs\VideoCutEditor"
InstallDirRegKey HKCU "Software\VideoCutEditor" "InstallDir"
Icon "..\src\VideoCutEditor\Assets\AppIcon.ico"
UninstallIcon "..\src\VideoCutEditor\Assets\AppIcon.ico"
BrandingText "VideoCutEditor ${APP_VERSION}"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1041 "ProductName" "VideoCutEditor"
VIAddVersionKey /LANG=1041 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1041 "FileDescription" "VideoCutEditor Setup"
VIAddVersionKey /LANG=1041 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1041 "LegalCopyright" "Copyright (c) 2026 VideoCutEditor contributors"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\VideoCutEditor.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch VideoCutEditor"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\third-party\Microsoft.WindowsAppSDK.txt"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Japanese"

Section "VideoCutEditor" SecApplication
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /oname=VideoCutEditor.exe "${SOURCE_DIR}\VideoCutEditor.exe"
  File "..\distribution\README.md"
  File "..\LICENSE"

  SetOutPath "$INSTDIR\licenses"
  File "..\third-party\Microsoft.WindowsAppSDK.txt"
  File "..\third-party\CommunityToolkit.Mvvm.txt"
  File "..\third-party\CommunityToolkit.Mvvm-ThirdPartyNotices.txt"
  File "..\third-party\dotnet-Windows.txt"
  File "..\third-party\dotnet-ThirdPartyNotices.txt"
  File "..\third-party\NSIS.txt"

  CreateDirectory "$SMPROGRAMS\VideoCutEditor"
  CreateShortcut "$SMPROGRAMS\VideoCutEditor\VideoCutEditor.lnk" "$INSTDIR\VideoCutEditor.exe" "" "$INSTDIR\VideoCutEditor.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "Software\VideoCutEditor" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "DisplayName" "VideoCutEditor"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "Publisher" "kbt-tesc"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "DisplayIcon" "$INSTDIR\VideoCutEditor.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "URLInfoAbout" "https://github.com/kbt-tesc/VideoCutEditor"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  Delete "$SMPROGRAMS\VideoCutEditor\VideoCutEditor.lnk"
  RMDir "$SMPROGRAMS\VideoCutEditor"

  Delete "$INSTDIR\licenses\Microsoft.WindowsAppSDK.txt"
  Delete "$INSTDIR\licenses\CommunityToolkit.Mvvm.txt"
  Delete "$INSTDIR\licenses\CommunityToolkit.Mvvm-ThirdPartyNotices.txt"
  Delete "$INSTDIR\licenses\dotnet-Windows.txt"
  Delete "$INSTDIR\licenses\dotnet-ThirdPartyNotices.txt"
  Delete "$INSTDIR\licenses\NSIS.txt"
  RMDir "$INSTDIR\licenses"
  Delete "$INSTDIR\VideoCutEditor.exe"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoCutEditor"
  DeleteRegKey HKCU "Software\VideoCutEditor"
SectionEnd
