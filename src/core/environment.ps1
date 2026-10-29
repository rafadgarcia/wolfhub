function Get-WolfHubEnvironment {
    $windowsPlatform = $false
    if ($PSVersionTable.PSVersion.Major -ge 6) { $windowsPlatform = $IsWindows }
    else { $windowsPlatform = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT }

    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) { $winget = Get-Command winget -ErrorAction SilentlyContinue }

    $isAdmin = $false
    if ($windowsPlatform) {
        try {
            $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
            $principal = New-Object Security.Principal.WindowsPrincipal($identity)
            $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        } catch { $isAdmin = $false }
    }

    [pscustomobject]@{
        IsWindows = $windowsPlatform
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        WingetPath = if ($winget) { $winget.Source } else { $null }
        IsAdmin = $isAdmin
        IsInteractive = [Environment]::UserInteractive -and -not [Console]::IsInputRedirected
    }
}
