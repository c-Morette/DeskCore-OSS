# Gera certificado self-signed para testar o stack Docker localmente (HTTPS).
# Requisito: OpenSSL no PATH (vem com o Git for Windows: C:\Program Files\Git\usr\bin).
# Uso: pwsh docker/nginx/generate-dev-certs.ps1

$ErrorActionPreference = "Stop"
$certDir = Join-Path $PSScriptRoot "certs"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null

$crt = Join-Path $certDir "fullchain.pem"
$key = Join-Path $certDir "privkey.pem"

& openssl req -x509 -nodes -newkey rsa:2048 `
    -keyout $key -out $crt -days 825 `
    -subj "/CN=localhost" `
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

Write-Host "Certificado self-signed gerado em: $certDir" -ForegroundColor Green
Write-Host "  - $crt"
Write-Host "  - $key"
Write-Host "O navegador vai avisar que e nao-confiavel (esperado em dev). Aceite a excecao." -ForegroundColor Yellow
