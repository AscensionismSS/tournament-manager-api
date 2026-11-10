$lines = Get-Content 'C:\Users\gabri\.gemini\antigravity\brain\003b5ca1-dc28-4388-8634-6419e0e937ca\.system_generated\tasks\task-1959.log'
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -like '*DbUpdateConcurrencyException*') {
        $start = [Math]::Max(0, $i - 30)
        $end = [Math]::Min($lines.Count - 1, $i + 15)
        Write-Host "=== CONCURRENCY ERROR FOUND AT LINE $i ==="
        for ($j = $start; $j -le $end; $j++) {
            Write-Host $lines[$j]
        }
    }
}

