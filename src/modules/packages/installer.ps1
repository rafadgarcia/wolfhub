function Get-WolfHubInstalledIds {
    if (-not $script:WolfHubEnvironment.WingetPath) { return @() }
    try {
        $lines = & $script:WolfHubEnvironment.WingetPath list --disable-interactivity 2>$null
        $output = $lines -join "`n"
        $ids = @{}
        foreach ($package in $script:WolfHubCatalog.packages) {
            if ($output -match [regex]::Escape($package.wingetId)) { $ids[$package.wingetId] = $true }
        }
        return @($ids.Keys)
    } catch { return @() }
}

function Invoke-WolfHubPackageSelection {
    param([string]$Title = 'Software Installer', [object[]]$Packages = $script:WolfHubCatalog.packages)
    if (-not $script:WolfHubEnvironment.WingetPath) {
        Write-WolfHubHeader; Show-WolfHubMessage 'WinGet is not available. Install it from Microsoft Store or Windows App Installer, then restart WolfHub.' Warning; [void](Read-Host 'Press Enter'); return
    }
    $selected = @{}
    $cursorIndex = 0
    Write-WolfHubHeader; Show-WolfHubMessage 'Checking installed applications with WinGet...' Info
    $installed = @(Get-WolfHubInstalledIds)
    while ($true) {
        Write-WolfHubHeader
        Write-Host "`n$Title`nSpace marks/unmarks | Enter reviews selection | A selects all | C clears | Esc goes back`n" -ForegroundColor Cyan
        $rows = @()
        $orderedPackages = @($Packages | Where-Object { $installed -notcontains $_.wingetId }) + @($Packages | Where-Object { $installed -contains $_.wingetId })
        foreach ($package in $orderedPackages) {
            $mark = if ($selected.ContainsKey($package.id)) {'x'} else {' '}
            $isInstalled = $installed -contains $package.wingetId
            $status = if ($isInstalled) {'  (installed)'} else {''}
            $rows += [pscustomobject]@{ Label=("[{0}] {1}{2}" -f $mark,$package.name,$status); Package=$package; IsInstalled=$isInstalled }
        }
        $choice = Read-WolfHubMenuSelection -Prompt 'Packages' -Items $rows -EnterAction Confirm -StartIndex $cursorIndex
        if ($choice -eq '0') { return }
        if ($choice -match '^space:(\d+)$') {
            $rowIndex = [int]$Matches[1]
            if ($rowIndex -lt 0 -or $rowIndex -ge $rows.Count) { continue }
            $cursorIndex = $rowIndex
            $row = $rows[$rowIndex]
            if ($selected.ContainsKey($row.Package.id)) { $selected.Remove($row.Package.id) } else { $selected[$row.Package.id] = $true }
            continue
        }
        if ($choice -match '^\d+$' -and [int]$choice -ge 0 -and [int]$choice -lt $rows.Count) {
            $cursorIndex = [int]$choice
            $row = $rows[$cursorIndex]
            if ($selected.ContainsKey($row.Package.id)) { $selected.Remove($row.Package.id) } else { $selected[$row.Package.id] = $true }
            continue
        }
        if ($choice -match '^(a|all)$') { foreach ($p in $Packages) { $selected[$p.id]=$true }; continue }
        if ($choice -match '^(c|clear)$') { $selected.Clear(); continue }
        if ($choice -eq 'confirm') {
            $toInstall = @($Packages | Where-Object { $selected.ContainsKey($_.id) -and $installed -notcontains $_.wingetId })
            if (-not $toInstall.Count) { Show-WolfHubMessage 'No selected packages need installation.' Warning; Start-Sleep -Milliseconds 800; continue }
            Write-WolfHubHeader; Write-Host "`nReview installation" -ForegroundColor Cyan
            $toInstall | ForEach-Object { Write-Host " • $($_.name) ($($_.wingetId))" }
            if (-not (Confirm-WolfHub 'Install these packages using WinGet?')) { continue }
            Invoke-WolfHubInstallBatch -Packages $toInstall
            [void](Read-Host 'Press Enter to continue'); return
        }
    }
}

function Invoke-WolfHubInstallBatch {
    param([object[]]$Packages)
    $i=0
    foreach ($package in $Packages) {
        $i++; Write-Progress -Activity 'WolfHub package installation' -Status "$($package.name) ($i/$($Packages.Count))" -PercentComplete ([int](100*$i/$Packages.Count))
        $args = @('install','--exact','--id',$package.wingetId,'--accept-package-agreements','--accept-source-agreements','--disable-interactivity')
        if ($package.scope -eq 'user') { $args += @('--scope','user') }
        try {
            & $script:WolfHubEnvironment.WingetPath @args
            $code = $LASTEXITCODE
            $result = if ($code -eq 0) {'success'} else {'failed'}
            Write-WolfHubLog -Directory $script:WolfHubSettings.logDirectory -Action 'install' -Module $package.wingetId -Result $result -ExitCode $code -Message $(if ($code -eq 0) {'Installed or already current.'} else {'WinGet returned a non-zero exit code.'})
            Show-WolfHubMessage ("{0}: {1} (exit {2})" -f $package.name,$result,$code) $(if ($code -eq 0) {'Success'} else {'Error'})
        } catch {
            Write-WolfHubLog -Directory $script:WolfHubSettings.logDirectory -Action 'install' -Module $package.wingetId -Result 'error' -ExitCode $null -Message $_.Exception.Message
            Show-WolfHubMessage ("{0}: {1}" -f $package.name,$_.Exception.Message) Error
        }
    }
    Write-Progress -Activity 'WolfHub package installation' -Completed
}

function Invoke-WolfHubInstaller {
    $packages = @($script:WolfHubCatalog.packages)
    foreach ($category in @($packages.category | Sort-Object -Unique)) {
        $group = @($packages | Where-Object category -EQ $category)
        Write-Verbose "Validated catalog group $category with $($group.Count) packages"
    }
    Invoke-WolfHubPackageSelection -Title 'Software Installer' -Packages $packages
}
