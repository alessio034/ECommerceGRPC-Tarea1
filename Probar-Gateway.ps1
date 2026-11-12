#Requires -Version 7
# Probar-Gateway.ps1 · autoverificación de la práctica de la Clase 2
# Uso (PowerShell 7, desde la carpeta del kit, con «docker compose up -d --build» ya hecho):
#     .\Probar-Gateway.ps1
# Cada prueba dice qué TODO revisar si no pasa. Para repetirla, espere 30 segundos
# (el límite de escrituras del TODO 4 se reinicia cada 30 segundos).
param([string]$Gateway = "http://localhost:5000")

$script:pasa = 0
$script:total = 0

function Pedir([string]$metodo, [string]$ruta, $cuerpo = $null, [string]$token = "") {
    $h = @{}
    if ($token) { $h["Authorization"] = "Bearer $token" }
    $p = @{ Method = $metodo; Uri = "$Gateway$ruta"; Headers = $h; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
    if ($null -ne $cuerpo) { $p.Body = ($cuerpo | ConvertTo-Json); $p.ContentType = "application/json" }
    try { return Invoke-WebRequest @p } catch { return $null }
}

function Prueba([string]$nombre, [string]$todo, [bool]$ok) {
    $script:total++
    if ($ok) { $script:pasa++; Write-Host ("PASA   {0}" -f $nombre) -ForegroundColor Green }
    else     { Write-Host ("FALLA  {0}   (revise el {1})" -f $nombre, $todo) -ForegroundColor Yellow }
}

$salud = Pedir GET "/health"
if ($null -eq $salud -or $salud.StatusCode -ne 200) {
    Write-Host "El gateway no responde en $Gateway. Revise «docker compose ps» en la carpeta Infrastructure." -ForegroundColor Red
    exit 1
}

function Token([string]$email, [string]$clave) {
    $r = Pedir POST "/api/auth/login" @{ email = $email; clave = $clave }
    if ($r -and $r.StatusCode -eq 200) { return ($r.Content | ConvertFrom-Json).token }
    return ""
}
$admin = Token "admin@example.com" "Admin2026!"
$cliente = Token "user@example.com" "Cliente2026!"
if (-not $admin -or -not $cliente) {
    Write-Host "No se pudo iniciar sesión con los usuarios de demostración (POST /api/auth/login)." -ForegroundColor Red
    exit 1
}

# TODO 1 · cliente gRPC de Productos registrado
$r = Pedir GET "/api/v1/productos"
Prueba "1. GET /api/v1/productos responde 200 con la lista" "TODO 1" ($r -and $r.StatusCode -eq 200)

# TODO 2 · GET por id
$r = Pedir GET "/api/v1/productos/1"
$p = if ($r -and $r.StatusCode -eq 200) { $r.Content | ConvertFrom-Json } else { $null }
Prueba "2. GET /api/v1/productos/1 responde 200 con el producto 1" "TODO 2" ($null -ne $p -and $p.id -eq 1 -and $p.nombre)
$r = Pedir GET "/api/v1/productos/99999"
Prueba "3. GET /api/v1/productos/99999 responde 404" "TODO 2" ($r -and $r.StatusCode -eq 404)

# TODO 3a · transformación de la respuesta
Prueba "4. La respuesta trae precio, categoria, stock y disponible" "TODO 3a" (
    $null -ne $p -and $null -ne $p.precio -and $p.categoria -and $null -ne $p.stock -and $null -ne $p.disponible)

# TODO 5 · PUT protegido con JWT y rol Admin
$stock = if ($null -ne $p -and $null -ne $p.stock) { [int]$p.stock } else { 10 }
$r = Pedir PUT "/api/v1/productos/1/stock" @{ cantidad = $stock }
Prueba "5. PUT /stock sin token responde 401" "TODO 5" ($r -and $r.StatusCode -eq 401)
$r = Pedir PUT "/api/v1/productos/1/stock" @{ cantidad = $stock } $cliente
Prueba "6. PUT /stock con el token de Cliente responde 403" "TODO 5" ($r -and $r.StatusCode -eq 403)
$r = Pedir PUT "/api/v1/productos/1/stock" @{ cantidad = $stock } $admin
$ok = $r -and $r.StatusCode -eq 200 -and ($r.Content | ConvertFrom-Json).id -eq 1
Prueba "7. PUT /stock con el token de Admin responde 200" "TODO 3b y TODO 5" $ok

# TODO 4 · límite de escrituras: 3 cada 30 s por usuario (la prueba 7 ya usó una)
$codigos = 1..3 | ForEach-Object { (Pedir PUT "/api/v1/productos/1/stock" @{ cantidad = $stock } $admin).StatusCode }
Prueba "8. La cuarta escritura en 30 s responde 429 ($($codigos -join ', '))" "TODO 4" ($codigos[-1] -eq 429)

Write-Host ""
$color = if ($script:pasa -eq $script:total) { "Green" } else { "Yellow" }
Write-Host ("Resultado: PASA {0}/{1}" -f $script:pasa, $script:total) -ForegroundColor $color
exit ([int]($script:pasa -ne $script:total))
