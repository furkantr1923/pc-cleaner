; =====================================================================
;  PC CLEANER — Windows kurulum betiği (NSIS 3)
;  Kullanıcı bazlı kurulum: YÖNETİCİ İZNİ GEREKMEZ.
;  Derleme:  makensis -DAPP_EXE=<PCCleaner.exe yolu> -DOUT_FILE=<çıktı> PcCleaner.nsi
; =====================================================================

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!define APP_NAME      "PC CLEANER"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "Furkan"
!define APP_EXE_NAME  "PCCleaner.exe"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\PCCleaner"

!ifndef APP_EXE
  !define APP_EXE "..\artifacts\portable\PCCleaner.exe"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "..\artifacts\PCCleaner_Setup_${APP_VERSION}.exe"
!endif

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\PC CLEANER"
InstallDirRegKey HKCU "Software\PCCleaner" "InstallDir"
BrandingText "${APP_NAME} ${APP_VERSION} · Geliştirici: ${APP_PUBLISHER}"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1055 "ProductName"     "${APP_NAME}"
VIAddVersionKey /LANG=1055 "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1055 "FileDescription" "${APP_NAME} Kurulumu"
VIAddVersionKey /LANG=1055 "FileVersion"     "${APP_VERSION}"
VIAddVersionKey /LANG=1055 "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey /LANG=1055 "LegalCopyright"  "© 2026 ${APP_PUBLISHER}"

!include "MUI2.nsh"

!define MUI_ICON   "..\src\PcCleaner.App\Assets\pccleaner.ico"
!define MUI_UNICON "..\src\PcCleaner.App\Assets\pccleaner.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "PC CLEANER Kurulumuna Hoş Geldiniz"
!define MUI_WELCOMEPAGE_TEXT "Bu sihirbaz PC CLEANER ${APP_VERSION} uygulamasını bilgisayarınıza kuracak.$\r$\n$\r$\nPC CLEANER; ücretsiz, reklamsız ve çevrimdışı çalışan basit bir geçici dosya temizleyicidir. Yönetici izni gerektirmez.$\r$\n$\r$\nDevam etmek için İleri'ye tıklayın."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE_NAME}"
!define MUI_FINISHPAGE_RUN_TEXT "PC CLEANER'ı şimdi başlat"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Turkish"

Section "PC CLEANER" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "/oname=${APP_EXE_NAME}" "${APP_EXE}"
  WriteUninstaller "$INSTDIR\Kaldir.exe"

  CreateDirectory "$SMPROGRAMS\PC CLEANER"
  CreateShortcut "$SMPROGRAMS\PC CLEANER\PC CLEANER.lnk" "$INSTDIR\${APP_EXE_NAME}"
  CreateShortcut "$SMPROGRAMS\PC CLEANER\PC CLEANER'ı Kaldır.lnk" "$INSTDIR\Kaldir.exe"
  CreateShortcut "$DESKTOP\PC CLEANER.lnk" "$INSTDIR\${APP_EXE_NAME}"

  WriteRegStr HKCU "Software\PCCleaner" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" "$\"$INSTDIR\Kaldir.exe$\""
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\${APP_EXE_NAME}"
  Delete "$INSTDIR\Kaldir.exe"
  RMDir  "$INSTDIR"
  Delete "$SMPROGRAMS\PC CLEANER\PC CLEANER.lnk"
  Delete "$SMPROGRAMS\PC CLEANER\PC CLEANER'ı Kaldır.lnk"
  RMDir  "$SMPROGRAMS\PC CLEANER"
  Delete "$DESKTOP\PC CLEANER.lnk"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\PCCleaner"
SectionEnd
