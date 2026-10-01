Add-Type -AssemblyName System.Drawing

$src = "C:\Users\RECLUSE\.gemini\antigravity-acp\brain\c67db600-870c-4593-97e2-d066bde49323\agdubugdu_app_icon_1790872953307.jpg"
$destPng = "D:\Projects\AgduBugdu\src\AgduBugdu.App\Assets\agdubugdu-logo.png"
$destIco = "D:\Projects\AgduBugdu\src\AgduBugdu.App\Assets\agdubugdu-logo.ico"

$img = [System.Drawing.Image]::FromFile($src)
$img.Save($destPng, [System.Drawing.Imaging.ImageFormat]::Png)

$bmp = New-Object System.Drawing.Bitmap $img, 256, 256
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::OpenWrite($destIco)
$icon.Save($fs)
$fs.Close()

$img.Dispose()
$bmp.Dispose()

Write-Host "Icons generated successfully at $destPng and $destIco"
