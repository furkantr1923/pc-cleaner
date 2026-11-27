# PC CLEANER — Test Raporu (v1.0.0)

Yalnızca **gerçekten çalıştırılan** testler raporlanmıştır.

## Özet

| Ortam | Sonuç |
|---|---|
| Linux, yetkisiz kullanıcı (.NET 10.0.404) | **39 / 39 geçti** |
| Linux, root | 37 geçti · 2 atlandı (izin testleri root'ta anlamsız) · 0 hata |
| **Gerçek Windows** (GitHub Actions `windows-latest`) | 37 geçti · 2 atlandı (Unix'e özel izin testleri) · **0 hata** |
| **Windows GUI açılış testi** (CI) | Ana pencere ~0,6 sn'de açıldı · tarama çalıştı · X (SC_CLOSE) ile düzgün kapandı |
| Derleme | 0 uyarı, 0 hata (uyarılar hata sayılıyor) |

## Kapsam

| Gereksinim | Test(ler) | Durum |
|---|---|---|
| Uygulama açılışı | CI `smoke-test.ps1` | ✅ Windows |
| Geçici klasör tespiti | `GetDefault_ReturnsOnlyExistingSafeAndDistinctLocations`, `Filter_RemovesDuplicatesNestedMissingAndUnsafe` | ✅ Linux + Windows |
| Dosya tarama (iç içe klasörler) | `Scan_FindsFilesInNestedFolders_AndSumsSizes` | ✅ |
| Boyut hesabı / Türkçe biçim | aynı test + `SizeFormatterTests` (6 durum) | ✅ |
| Tarama iptali | `Scan_CanBeCancelled` | ✅ |
| Temizleme onayı / iptali | UI: onay penceresi varsayılanı **Hayır**; Core: `Delete_EmptySelection_DeletesNothing`, `Delete_CancelledBeforeStart_DeletesNothing` | ✅ Core · UI onay penceresi elle doğrulanmalı* |
| Seçili dosyaların silinmesi | `Delete_RemovesOnlySelectedEligibleFiles` | ✅ |
| Kilitli dosya | `Delete_LockedFile_IsSkippedAsInUse_OthersStillDeleted` | ✅ Linux + Windows |
| İzin reddi | `Delete_PermissionDenied_IsSkippedAndReported`, `Scan_InaccessibleDirectory_IsCountedAndDoesNotCrash` | ✅ Linux (yetkisiz kullanıcı) |
| Yol doğrulama / traversal | `PathGuardTests` (8), `Delete_PathTraversal_IsRejected` | ✅ |
| Onaylı klasör dışı koruma | `Delete_FileOutsideApprovedRoot_IsRejected`, `Delete_FileFromUnapprovedLocation_IsRejected`, `Scan_NeverReturnsFilesOutsideApprovedRoot`, `Scan_UnsafeRoot_IsIgnored` | ✅ |
| Symlink / reparse point | `Scan_DoesNotFollowSymbolicLinks`, `Delete_ThroughSymlinkedFolder_…`, `Delete_SymlinkFile_…` | ✅ Linux + Windows |
| Salt okunur / değişmiş dosya | `Scan_SkipsReadOnlyFiles`, `Delete_FileBecameReadOnlyAfterScan_IsNotTouched` | ✅ |
| Kökün / klasörün silinmesi engeli | `Delete_RootItself_IsRejected`, `Delete_DirectoryPath_IsRejected` | ✅ |
| Son 24 saat koruması | `Scan_SkipsRecentFiles_ByDefault` | ✅ |

Tüm silme testleri, her test için oluşturulan **sahte Temp klasöründe** çalışır; gerçek kişisel dosyalara dokunulmaz.

## Bulunan ve düzeltilen sorun

- Windows'ta `%TEMP%` 8.3 kısa adla (`C:\Users\RUNNER~1\…`) gelebiliyor → aynı klasör iki kez listelenebilirdi. `PathGuard.ToLongPath` eklendi; CI ekran görüntüsünde tek grup görünüyor.

## Çalıştırılamayanlar (dürüst not)

- \* Onay penceresi ve "Temizlemeyi Başlat" tıklaması otomatik UI testiyle **tıklanmadı** (CI'da gerçek silme yapılmaması için bilinçli). Mantık Core testleriyle doğrulandı.
- Windows'ta ACL ile "erişim reddedildi" senaryosu otomatik test edilmedi (Linux'ta yetkisiz kullanıcıyla doğrulandı).
- Setup.exe kurulumu bir Windows makinede çalıştırılarak denenmedi; içindeki EXE'nin portable EXE ile birebir aynı olduğu SHA-256 ile doğrulandı.
