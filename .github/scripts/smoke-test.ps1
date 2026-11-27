# PC CLEANER — GUI açılış testi (yalnızca CI için).
# Uygulamayı açar, ana pencereyi doğrular, "Taramayı Başlat"a basar ve ekran görüntüsü alır.
# Silme işlemi YAPMAZ.
param([string]$Exe = "artifacts/portable/PCCleaner.exe", [string]$OutDir = "screenshots")
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@

function Save-Window([IntPtr]$h, [string]$path) {
  $r = New-Object Win+RECT
  [Win]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $hgt = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap $w, $hgt
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc(); [Win]::PrintWindow($h, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Host "Kaydedildi: $path ($w x $hgt)"
}

$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Exe -PassThru
$h = [IntPtr]::Zero
while ($sw.Elapsed.TotalSeconds -lt 30) {
  Start-Sleep -Milliseconds 250
  $p.Refresh()
  if ($p.HasExited) { throw "Uygulama beklenmedik şekilde kapandı (çıkış kodu $($p.ExitCode))." }
  if ($p.MainWindowHandle -ne [IntPtr]::Zero -and $p.MainWindowTitle -eq "PC CLEANER") { $h = $p.MainWindowHandle; break }
}
if ($h -eq [IntPtr]::Zero) { throw "Ana pencere 30 sn içinde açılmadı." }
Write-Host "BAŞARILI: Ana pencere '$($p.MainWindowTitle)' $([int]$sw.ElapsedMilliseconds) ms'de açıldı."
Write-Host "Bellek (çalışma kümesi): $([int]($p.WorkingSet64/1MB)) MB"
Start-Sleep -Seconds 2
Save-Window $h "$OutDir/1-acilis.png"

# Enter = AcceptButton = "Taramayı Başlat"
[Win]::SetForegroundWindow($h) | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shell.AppActivate($p.Id) | Out-Null
Start-Sleep -Milliseconds 500
$shell.SendKeys("{ENTER}")
Start-Sleep -Seconds 15
$p.Refresh()
if ($p.HasExited) { throw "Tarama sırasında uygulama kapandı." }
Write-Host "BAŞARILI: Tarama sonrası uygulama çalışıyor."
Save-Window $h "$OutDir/2-tarama-sonucu.png"

# Kullanıcının X'e basmasıyla aynı yol: WM_SYSCOMMAND / SC_CLOSE
[Win]::SetForegroundWindow($h) | Out-Null
$posted = [Win]::PostMessage($h, 0x0112, [IntPtr]0xF060, [IntPtr]::Zero)
Write-Host "SC_CLOSE gönderildi: $posted"
if (-not $p.WaitForExit(8000)) {
  $shell.AppActivate($p.Id) | Out-Null; Start-Sleep -Milliseconds 300; $shell.SendKeys("%{F4}")
  Write-Host "Alt+F4 gönderildi"
}
if (-not $p.WaitForExit(8000)) {
  Save-Window $h "$OutDir/3-kapanmadi.png"
  Get-Process -Id $p.Id | Format-List Id, Responding, MainWindowTitle, MainWindowHandle | Out-String | Write-Host
  Stop-Process $p -Force
  throw "Uygulama kapanmadı."
}
Write-Host "BAŞARILI: Uygulama düzgün kapandı."
