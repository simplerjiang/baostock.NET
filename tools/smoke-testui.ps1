$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5050'

function Hit($name, $method, $path, $body) {
  $u = "$base$path"
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  try {
    if ($method -eq 'GET') {
      $r = Invoke-WebRequest -UseBasicParsing -Uri $u -Method Get -TimeoutSec 30
    } else {
      $r = Invoke-WebRequest -UseBasicParsing -Uri $u -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 60
    }
    $sc = [int]$r.StatusCode
    $c  = $r.Content
  } catch {
    if ($_.Exception.Response) {
      $sc = [int]$_.Exception.Response.StatusCode
      try {
        $stream = $_.Exception.Response.GetResponseStream()
        $sr = New-Object System.IO.StreamReader($stream)
        $c = $sr.ReadToEnd()
      } catch { $c = $_.Exception.Message }
    } else { $sc = 'ERR'; $c = $_.Exception.Message }
  }
  $sw.Stop()
  $preview = if ($c.Length -gt 380) { $c.Substring(0,380) + '...(truncated)' } else { $c }
  "==[$name]== HTTP=$sc  $method $path  client=$($sw.ElapsedMilliseconds)ms"
  "  body: $body"
  "  resp: $preview"
}

Hit '01-meta'         'GET'  '/api/meta/endpoints' $null
Hit '02-status0'      'GET'  '/api/session/status' $null
Hit '03-login'        'POST' '/api/session/login'  '{}'
Hit '04-trade-dates'  'POST' '/api/baostock/metadata/trade-dates' '{"startDate":"2025-01-01","endDate":"2025-01-15"}'
Hit '05-kdata'        'POST' '/api/baostock/history/k-data-plus'  '{"code":"sh.600519","fields":"date,code,open,close","startDate":"2025-01-01","endDate":"2025-01-10","frequency":"Day","adjustFlag":"PreAdjust"}'
Hit '06-profit'       'POST' '/api/baostock/evaluation/profit-data' '{"code":"sh.600519","year":2024,"quarter":1}'
Hit '07-rt-quote'     'POST' '/api/multi/realtime-quote' '{"code":"SH600519"}'
Hit '08-multi-k'      'POST' '/api/multi/history-k-line' '{"code":"SH600519","frequency":"Day","startDate":"2025-01-01","endDate":"2025-01-15","adjust":"PreAdjust"}'
Hit '09-err-date'     'POST' '/api/baostock/metadata/trade-dates' '{"startDate":"INVALID","endDate":"2025-01-15"}'
Hit '10-logout'       'POST' '/api/session/logout' '{}'
