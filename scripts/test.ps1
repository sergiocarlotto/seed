<#
.SYNOPSIS
    Roda `dotnet test` dentro de um container Linux (.NET SDK), contornando o
    bloqueio do Smart App Control (SAC) do Windows.

.DESCRIPTION
    No Windows com Smart App Control LIGADO, o `dotnet test` falha nos testes de
    integracao: o `testhost` carrega o Seed.Infrastructure.dll recem-compilado
    (assembly sem assinatura) e o SAC bloqueia o carregamento
    (System.IO.FileLoadException, 0x800711C7). O SAC avalia por hash, entao todo
    rebuild gera um binario novo que volta a ser bloqueado.

    Este wrapper roda o `dotnet test` dentro de mcr.microsoft.com/dotnet/sdk:10.0,
    onde o SAC nao se aplica. O repo e montado (os resultados aparecem no host) e
    o socket do Docker do host e montado para que o Testcontainers consiga subir o
    Postgres real. O TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal faz o codigo
    de teste (dentro do container) alcancar o Postgres publicado no host.

    Um volume nomeado (seed-nuget, compartilhado com ef.ps1) faz cache dos pacotes.

.EXAMPLE
    scripts/test.ps1
    scripts/test.ps1 --filter FullyQualifiedName~AccessControlEnforcementTests
#>

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path "$PSScriptRoot/..").Path
$argLine = $args -join ' '

$inner = @"
set -e
dotnet test Seed.slnx $argLine
"@

# O here-string herda CRLF no checkout Windows; o bash dentro do container
# quebra com o \r ao final de cada linha. Normaliza para LF.
$inner = $inner -replace "`r`n", "`n"

docker run --rm `
    -v "${repo}:/src" `
    -v "seed-nuget:/root/.nuget/packages" `
    -v "/var/run/docker.sock:/var/run/docker.sock" `
    -e "TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal" `
    --add-host "host.docker.internal:host-gateway" `
    -w /src/apps/api `
    mcr.microsoft.com/dotnet/sdk:10.0 `
    bash -c $inner
