function Invoke-WolfHubMainMenu {
    $items = @(
        [pscustomobject]@{ Key='01'; Label='Software Installer' },
        [pscustomobject]@{ Key='02'; Label='Windows Tweaks' },
        [pscustomobject]@{ Key='03'; Label='Debloat' },
        [pscustomobject]@{ Key='04'; Label='My Toolkit' },
        [pscustomobject]@{ Key='05'; Label='Profiles' },
        [pscustomobject]@{ Key='06'; Label='Settings' }
    )
    while ($true) {
        Write-WolfHubHeader
        Write-WolfHubFooter
        $mainItems = $items + @([pscustomobject]@{Key='00';Label='Exit'})
        $choice = Read-WolfHubMenuSelection -Prompt 'Main menu' -Items $mainItems
        if ($choice -match '^digit:(\d)$') {
            $number = [int]$Matches[1]
            if ($number -eq 0) { return }
            if ($number -le $items.Count) { $choice = [string]($number - 1) }
            else { $choice = 'invalid' }
        }
        if ($choice -eq '00' -or $choice -eq [string]($mainItems.Count - 1) -or $choice -match '^(q|quit|exit)$') { return }
        $item = $null
        if ($choice -match '^\d+$' -and [int]$choice -ge 0 -and [int]$choice -lt $items.Count) { $item = $items[[int]$choice] }
        if (-not $item) { $item = $items | Where-Object Key -EQ $choice | Select-Object -First 1 }
        if ($item) {
            switch ($item.Key) {
                '01' { Invoke-WolfHubInstaller }
                '02' { Show-WolfHubComingSoon 'Windows Tweaks' }
                '03' { Show-WolfHubComingSoon 'Debloat' }
                '04' { Show-WolfHubComingSoon 'My Toolkit' }
                '05' { Invoke-WolfHubProfiles }
                '06' { Invoke-WolfHubSettings }
            }
        }
        else { Show-WolfHubMessage 'Invalid selection.' Warning; Start-Sleep -Milliseconds 700 }
    }
}

function Show-WolfHubComingSoon {
    param([string]$Name)
    Write-WolfHubHeader
    Write-Host "`n$Name`n`nComing Soon. This module is not implemented yet." -ForegroundColor Gray
    [void](Read-Host 'Press Enter to return')
}

function Invoke-WolfHubSettings {
    while ($true) {
        Write-WolfHubHeader
        Write-Host "`nSettings`n[1] Toggle Unicode art: $($script:WolfHubSettings.showArt)`n[2] Toggle install confirmation: $($script:WolfHubSettings.confirmInstalls)`n[3] Change log directory`n[0] Back" -ForegroundColor White
        $choice = Read-WolfHubChoice
        switch ($choice) {
            '1' { $script:WolfHubSettings.showArt = -not $script:WolfHubSettings.showArt }
            '2' { $script:WolfHubSettings.confirmInstalls = -not $script:WolfHubSettings.confirmInstalls }
            '3' { $newPath = Read-Host 'Log directory'; if (-not [string]::IsNullOrWhiteSpace($newPath)) { $script:WolfHubSettings.logDirectory = $newPath } }
            '0' { Save-WolfHubSettings -Settings $script:WolfHubSettings -Root $script:WolfHubRoot; return }
        }
    }
}

function Invoke-WolfHubProfiles {
    $profiles = @($script:WolfHubProfiles.Values | Sort-Object id)
    while ($true) {
        Write-WolfHubHeader
        Write-Host "`nProfiles" -ForegroundColor Cyan
        $profileItems = @($profiles | ForEach-Object { [pscustomobject]@{ Key=[string]$_.id; Label=[string]$_.name } })
        $profileItems += [pscustomobject]@{ Key='back'; Label='Back' }
        $choice = Read-WolfHubMenuSelection -Prompt 'Choose a profile' -Items $profileItems
        if ($choice -match '^digit:(\d)$') {
            $number = [int]$Matches[1]
            if ($number -eq 0) { return }
            if ($number -le $profiles.Count) { $choice = [string]($number - 1) }
            else { $choice = 'invalid' }
        }
        if ($choice -eq 'back' -or $choice -eq [string]($profileItems.Count - 1)) { return }
        $index = 0
        if ($choice -match '^\d+$' -and [int]$choice -ge 0 -and [int]$choice -lt $profiles.Count) { $index = [int]$choice + 1 }
        elseif ([int]::TryParse($choice, [ref]$index)) { }
        elseif ($choice -match '^(minimal|personal|support)$') { $index = [array]::IndexOf(@($profiles.id), $choice) + 1 }
        else { $index = 0 }
        if ($index -ge 1 -and $index -le $profiles.Count) {
            $profile = $profiles[$index-1]
            $packages = @($profile.packages | ForEach-Object { $id=$_; $script:WolfHubCatalog.packages | Where-Object id -EQ $id })
            Invoke-WolfHubPackageSelection -Title $profile.name -Packages $packages
        }
    }
}
