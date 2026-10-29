$script:WolfHubColors = @{ Accent = 'Cyan'; Soft = 'DarkCyan'; Pink = 'Magenta'; Text = 'White'; Muted = 'Gray'; Success = 'Green'; Error = 'Red'; Warning = 'Yellow' }

function Get-WolfHubTerminalSize {
    try { return [pscustomobject]@{ Width = [Console]::WindowWidth; Height = [Console]::WindowHeight } }
    catch { return [pscustomobject]@{ Width = 80; Height = 25 } }
}

function Write-WolfHubHeader {
    Clear-Host
    $size = Get-WolfHubTerminalSize
    Write-Host '⟦ W O L F H U B ⟧' -NoNewline -ForegroundColor Cyan
    Write-Host '  v0.1.0' -ForegroundColor Gray
    Write-Host ('-' * [Math]::Min([Math]::Max($size.Width - 2, 1), 72)) -ForegroundColor DarkCyan
}

function Write-WolfHubFooter {
    Write-Host "`nUse number + Enter, or click an item. 0/Esc goes back." -ForegroundColor Gray
    Write-Host (" PS {0} | WinGet {1} | Admin {2}" -f $script:WolfHubEnvironment.PowerShellVersion, $(if ($script:WolfHubEnvironment.WingetPath) {'ready'} else {'missing'}), $(if ($script:WolfHubEnvironment.IsAdmin) {'yes'} else {'no'})) -ForegroundColor DarkGray
}

function Read-WolfHubChoice {
    param([string]$Prompt = 'Choice', [object[]]$Items = @(), [ValidateSet('Select','Confirm')][string]$EnterAction = 'Select', [int]$StartIndex = 0)
    if ($Items.Count -eq 0) { return (Read-Host $Prompt) }

    $interactiveConsole = $false
    try { $interactiveConsole = -not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected } catch { $interactiveConsole = $false }
    if (-not $interactiveConsole) { return (Read-Host $Prompt) }

    $supportsMouse = $false
    $mouseRawUi = $null
    try {
        $mouseRawUi = $Host.UI.RawUI
        $supportsMouse = $null -ne $mouseRawUi -and $Host.Name -eq 'ConsoleHost' -and $env:WT_SESSION
    } catch { $supportsMouse = $false }
    $cursorTop = [Console]::CursorTop
    $windowTop = [Console]::WindowTop
    $windowHeight = [Console]::WindowHeight
    $windowWidth = [Console]::WindowWidth
    $selectedIndex = [Math]::Max(0, [Math]::Min($StartIndex, $Items.Count - 1))
    $firstVisible = 0
    $lastVisible = 0

    while ($true) {
        $maxVisible = [Math]::Max(1, $windowHeight - 8)
        $visibleCount = [Math]::Min($maxVisible, $Items.Count)
        if ($selectedIndex -lt $firstVisible) { $firstVisible = $selectedIndex }
        if ($selectedIndex -ge ($firstVisible + $visibleCount)) { $firstVisible = $selectedIndex - $visibleCount + 1 }
        $lastVisible = $firstVisible + $visibleCount - 1
        $drawTop = [Math]::Max(0, [Math]::Min($cursorTop, $windowTop + $windowHeight - $visibleCount - 1))

        for ($i = $firstVisible; $i -le $lastVisible; $i++) {
            [Console]::SetCursorPosition(0, $drawTop + ($i - $firstVisible))
            $label = [string]$Items[$i].Label
            if ($Items[$i].PSObject.Properties['Key']) { $label = '[' + $Items[$i].Key + '] ' + $label }
            $line = if ($i -eq $selectedIndex) { '> ' + $label } else { '  ' + $label }
            $line = $line.PadRight([Math]::Max(1, $windowWidth - 1))
            if ($line.Length -ge $windowWidth) { $line = $line.Substring(0, $windowWidth - 1) }
            [Console]::SetCursorPosition(0, $drawTop + ($i - $firstVisible))
            if ($i -eq $selectedIndex) { [Console]::ForegroundColor = [ConsoleColor]::Black; [Console]::BackgroundColor = [ConsoleColor]::Cyan }
            elseif ($Items[$i].PSObject.Properties['IsInstalled'] -and $Items[$i].IsInstalled) { [Console]::ForegroundColor = [ConsoleColor]::DarkGray }
            else { [Console]::ForegroundColor = [ConsoleColor]::Gray }
            [Console]::Write($line)
            [Console]::ResetColor()
        }

        try { [Console]::SetCursorPosition(0, [Math]::Min($drawTop + $visibleCount, $windowTop + $windowHeight - 1)) } catch {}
        if ($supportsMouse) {
            try {
                $key = $mouseRawUi.ReadKey([System.Management.Automation.Host.ReadKeyOptions]::IncludeKeyDown -bor [System.Management.Automation.Host.ReadKeyOptions]::IncludeMouseDown -bor [System.Management.Automation.Host.ReadKeyOptions]::NoEcho)
                $keyType = [string]$key.GetType().Name
                if ($keyType -eq 'KeyInfo') {
                    if ($key.KeyDown -eq $false) { continue }
                    $virtualKey = [int]$key.VirtualKeyCode
                    $character = [string]$key.Character
                }
                else {
                    $virtualKey = [int]$key.VirtualKeyCode
                    $character = [string]$key.Character
                    if ($virtualKey -eq 0 -and $character -eq [char]0) {
                        $relativeY = $key.BufferY - $drawTop
                        if ($relativeY -ge 0 -and $relativeY -lt $visibleCount) { return [string]($firstVisible + $relativeY) }
                        continue
                    }
                }
                if ($keyType -ne 'KeyInfo' -and $virtualKey -eq 0) {
                    $relativeY = $key.BufferY - $drawTop
                    if ($relativeY -ge 0 -and $relativeY -lt $visibleCount) {
                        return [string]($firstVisible + $relativeY)
                    }
                    continue
                }
            } catch { $supportsMouse = $false }
        }
        if (-not $supportsMouse) {
            $key = [Console]::ReadKey($true)
            $virtualKey = [int]$key.Key
            $character = [string]$key.KeyChar
        }
        switch ($virtualKey) {
            38 { if ($selectedIndex -gt 0) { $selectedIndex-- }; continue }
            40 { if ($selectedIndex -lt ($Items.Count - 1)) { $selectedIndex++ }; continue }
            36 { $selectedIndex = 0; continue }
            35 { $selectedIndex = $Items.Count - 1; continue }
            27 { return '0' }
            13 { if ($EnterAction -eq 'Confirm') { return 'confirm' }; return [string]$selectedIndex }
            32 { return ('space:{0}' -f $selectedIndex) }
            default {
                if ($character -match '^\d$') {
                    if ($EnterAction -eq 'Select') { return ('digit:{0}' -f $character) }
                    return [string]$character
                }
                if ($character -match '^[a-zA-Z]$') { return $character.ToLowerInvariant() }
            }
        }
    }
}

function Read-WolfHubMenuSelection {
    param([string]$Prompt = 'Select an item', [object[]]$Items = @(), [ValidateSet('Select','Confirm')][string]$EnterAction = 'Select', [int]$StartIndex = 0)
    if ($Items.Count -eq 0) { return (Read-WolfHubChoice -Prompt $Prompt) }
    Write-Host "`n$Prompt" -ForegroundColor Cyan
    Write-Host 'Arrows move | Space selects | Enter confirms | Esc goes back' -ForegroundColor Gray
    return (Read-WolfHubChoice -Prompt $Prompt -Items $Items -EnterAction $EnterAction -StartIndex $StartIndex)
}

function Confirm-WolfHub {
    param([string]$Message)
    $answer = Read-Host ($Message + ' [y/N]')
    return $answer -match '^(y|yes|s|sim)$'
}

function Show-WolfHubMessage {
    param([string]$Message, [ValidateSet('Info','Success','Warning','Error')][string]$Kind = 'Info')
    $color = switch ($Kind) { Success {'Green'} Warning {'Yellow'} Error {'Red'} default {'Cyan'} }
    Write-Host $Message -ForegroundColor $color
}
