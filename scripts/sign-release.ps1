param([Parameter(Mandatory=$true)][string]$SigningKey,[string]$BaseUrl='http://8.140.49.4/aqserver')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $root 'VERSION'))).Trim()
$name='AQServer-Setup-'+$version+'.exe'
$installer=Join-Path $root ('artifacts/'+$name)
$releaseDir=Join-Path $root 'website/public/releases'
New-Item -ItemType Directory -Force -Path $releaseDir|Out-Null
$release=[ordered]@{appId='AQServer';version=$version;publishedUtc=[DateTime]::UtcNow.ToString('o');notes="托盘菜单与主窗口主题统一。`n新增完整目录安装器与关于、检查更新。`n偏好设置可应用，面板宽度可调整与记忆。`n底部布局固定，保留非线性数字与曲线动画。";installerUrl=$BaseUrl.TrimEnd('/')+'/releases/'+$name;installerSha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant();installerBytes=(Get-Item -LiteralPath $installer).Length}
$payload=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress))
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
$rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKey)))
try{$signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'));$envelope=[ordered]@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)};[IO.File]::WriteAllText((Join-Path $releaseDir 'stable.json'),($envelope|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding($false)))}finally{$rsa.Dispose()}
Copy-Item -LiteralPath $installer -Destination $releaseDir -Force
[IO.File]::WriteAllText((Join-Path $releaseDir 'SHA256SUMS.txt'),($release.installerSha256+'  '+$name+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
Write-Output ('SIGNED_RELEASE='+$version)
