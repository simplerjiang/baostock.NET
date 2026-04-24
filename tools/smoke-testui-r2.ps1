$ErrorActionPreference = 'Continue'
$b = 'http://localhost:5050'

"S1-pre-relogin: " + (Invoke-WebRequest -UseBasicParsing "$b/api/session/status").Content
"L1-relogin: " + ((Invoke-WebRequest -UseBasicParsing -Uri "$b/api/session/login" -Method Post -ContentType 'application/json' -Body '{}' -TimeoutSec 30).Content)

$tdr = (Invoke-WebRequest -UseBasicParsing -Uri "$b/api/baostock/metadata/trade-dates" -Method Post -ContentType 'application/json' -Body '{"startDate":"2024-12-20","endDate":"2024-12-31"}' -TimeoutSec 30).Content | ConvertFrom-Json
"TD-r2: ok=$($tdr.ok) elapsed=$($tdr.elapsedMs) rows=$($tdr.rowCount)"

$rq = (Invoke-WebRequest -UseBasicParsing -Uri "$b/api/multi/realtime-quote" -Method Post -ContentType 'application/json' -Body '{"code":"SZ000001"}' -TimeoutSec 30).Content | ConvertFrom-Json
"RQ-SZ000001: ok=$($rq.ok) src=$($rq.data.source) name=$($rq.data.name) last=$($rq.data.last)"

$sb = (Invoke-WebRequest -UseBasicParsing -Uri "$b/api/baostock/metadata/stock-basic" -Method Post -ContentType 'application/json' -Body '{}' -TimeoutSec 30).Content | ConvertFrom-Json
"SB-empty: ok=$($sb.ok) rows=$($sb.rowCount) errType=$($sb.errorType) err=$($sb.error)"

$bad = (Invoke-WebRequest -UseBasicParsing -Uri "$b/api/baostock/evaluation/profit-data" -Method Post -ContentType 'application/json' -Body '{"code":"NOPE000","year":2024,"quarter":1}' -TimeoutSec 30).Content | ConvertFrom-Json
"BAD-profit: ok=$($bad.ok) rows=$($bad.rowCount) errType=$($bad.errorType) err=$($bad.error)"

$lo = (Invoke-WebRequest -UseBasicParsing -Uri "$b/api/session/logout" -Method Post -ContentType 'application/json' -Body '{}' -TimeoutSec 30).Content
"LO: $lo"
"S2-post-logout: " + (Invoke-WebRequest -UseBasicParsing "$b/api/session/status").Content
