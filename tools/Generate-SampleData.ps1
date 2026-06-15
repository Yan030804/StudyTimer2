param([switch]$Overwrite)

$ErrorActionPreference = "Stop"

$dateLabel = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5pel5pyf"))
$totalDurationLabel = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5oC75a2m5Lmg5pe26ZW/"))
$totalSecondsLabel = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5oC756eS5pWw"))
$detailsLabel = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5a2m5Lmg5piO57uG"))

$root = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "StudyTimerData"
$samples = @(
    @{ Date = "2026-01-08"; Sessions = @(@("08:10:00", "09:25:00")) },
    @{ Date = "2026-01-22"; Sessions = @(@("09:00:00", "10:10:00"), @("19:30:00", "20:20:00")) },
    @{ Date = "2026-02-05"; Sessions = @(@("20:00:00", "20:45:00")) },
    @{ Date = "2026-02-18"; Sessions = @(@("08:30:00", "09:50:00"), @("14:00:00", "15:00:00")) },
    @{ Date = "2026-03-03"; Sessions = @(@("07:50:00", "09:20:00")) },
    @{ Date = "2026-03-17"; Sessions = @(@("08:00:00", "09:30:00"), @("19:00:00", "20:30:00")) },
    @{ Date = "2026-03-29"; Sessions = @(@("15:10:00", "16:00:00")) },
    @{ Date = "2026-04-04"; Sessions = @(@("09:00:00", "10:10:00"), @("14:20:00", "15:20:00")) },
    @{ Date = "2026-04-15"; Sessions = @(@("18:30:00", "20:10:00")) },
    @{ Date = "2026-04-27"; Sessions = @(@("08:30:00", "10:00:00"), @("20:00:00", "21:00:00")) },
    @{ Date = "2026-05-02"; Sessions = @(@("10:00:00", "11:00:00")) },
    @{ Date = "2026-05-11"; Sessions = @(@("08:00:00", "09:15:00"), @("19:00:00", "19:45:00")) },
    @{ Date = "2026-05-20"; Sessions = @(@("08:20:00", "10:00:00"), @("14:00:00", "15:35:00")) },
    @{ Date = "2026-05-30"; Sessions = @(@("09:10:00", "10:35:00")) },
    @{ Date = "2026-06-01"; Sessions = @(@("08:00:00", "09:30:00")) },
    @{ Date = "2026-06-03"; Sessions = @(@("14:00:00", "16:00:00")) },
    @{ Date = "2026-06-07"; Sessions = @(@("20:00:00", "20:45:00")) },
    @{ Date = "2026-06-08"; Sessions = @(@("08:10:00", "09:30:00"), @("19:00:00", "20:00:00")) },
    @{ Date = "2026-06-09"; Sessions = @(@("19:20:00", "20:30:00")) },
    @{ Date = "2026-06-10"; Sessions = @(@("08:00:00", "09:30:00"), @("14:00:00", "15:00:00")) },
    @{ Date = "2026-06-12"; Sessions = @(@("08:00:00", "09:40:00"), @("19:00:00", "20:20:00")) },
    @{ Date = "2026-06-13"; Sessions = @(@("09:00:00", "10:45:00")) },
    @{ Date = "2026-06-14"; Sessions = @(@("08:30:00", "09:45:00"), @("20:00:00", "21:00:00")) },
    @{ Date = "2026-06-15"; Sessions = @(@("08:20:00", "09:10:00"), @("14:00:00", "14:30:00")) }
)

for ($month = 1; $month -le 12; $month++) {
    $monthDirectory = Join-Path (Join-Path $root "2026") $month.ToString("00")
    New-Item -ItemType Directory -Force $monthDirectory | Out-Null
}

$created = 0
$skipped = 0

foreach ($sample in $samples) {
    $date = [DateTime]::ParseExact($sample.Date, "yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)
    $monthDirectory = Join-Path (Join-Path $root $date.Year.ToString("0000")) $date.Month.ToString("00")
    New-Item -ItemType Directory -Force $monthDirectory | Out-Null
    $path = Join-Path $monthDirectory ($sample.Date + ".txt")

    if ((Test-Path -LiteralPath $path) -and -not $Overwrite) {
        $skipped++
        continue
    }

    $entries = @()
    $totalSeconds = 0
    $sessionPairs = [Collections.ArrayList]::new()
    if ($sample.Sessions[0] -is [string]) {
        [void]$sessionPairs.Add(@($sample.Sessions[0], $sample.Sessions[1]))
    } else {
        foreach ($pair in $sample.Sessions) {
            [void]$sessionPairs.Add($pair)
        }
    }

    foreach ($session in $sessionPairs) {
        $startText = [string]$session[0]
        $endText = [string]$session[1]
        $start = [DateTime]::ParseExact($sample.Date + " " + $startText, "yyyy-MM-dd HH:mm:ss", [Globalization.CultureInfo]::InvariantCulture)
        $end = [DateTime]::ParseExact($sample.Date + " " + $endText, "yyyy-MM-dd HH:mm:ss", [Globalization.CultureInfo]::InvariantCulture)
        $duration = $end - $start
        $totalSeconds += [int]$duration.TotalSeconds
        $entries += @{ Start = $start; End = $end; Duration = $duration }
    }

    $total = [TimeSpan]::FromSeconds($totalSeconds)
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add($dateLabel + ": " + $sample.Date)
    $lines.Add(($totalDurationLabel + ": {0:00}:{1:00}:{2:00}" -f [math]::Floor($total.TotalHours), $total.Minutes, $total.Seconds))
    $lines.Add($totalSecondsLabel + ": " + $totalSeconds)
    $lines.Add("")
    $lines.Add($detailsLabel + ":")

    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        $durationText = "{0:00}:{1:00}:{2:00}" -f [math]::Floor($entry.Duration.TotalHours), $entry.Duration.Minutes, $entry.Duration.Seconds
        $lines.Add(("{0}. {1} -> {2} | {3}" -f ($index + 1), $entry.Start.ToString("yyyy-MM-dd HH:mm:ss"), $entry.End.ToString("yyyy-MM-dd HH:mm:ss"), $durationText))
    }

    [IO.File]::WriteAllLines($path, $lines, [Text.UTF8Encoding]::new($false))
    $created++
}

Write-Output "Root=$root"
Write-Output "Created=$created"
Write-Output "Skipped=$skipped"
