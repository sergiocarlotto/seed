<#
.SYNOPSIS
    Roda `dotnet ef` dentro de um container Linux (.NET SDK), contornando o
    bloqueio do Smart App Control (SAC) do Windows.

.DESCRIPTION
    No Windows com Smart App Control LIGADO, o `dotnet ef` falha ao gerar
    migrations: a ferramenta carrega por reflexao o Seed.Infrastructure.dll
    recem-compilado (assembly sem assinatura) e o SAC bloqueia esse
    carregamento. O SAC nao oferece exclusao por arquivo, e desliga-lo e
    irreversivel sem resetar o Windows.

    Este wrapper executa o mesmo `dotnet ef` dentro de um container Linux
    (mcr.microsoft.com/dotnet/sdk:10.0), onde o SAC nao se aplica. O repositorio
    e montado, entao os arquivos gerados (ex.: novas migrations) aparecem no host
    normalmente. Um volume nomeado (seed-nuget) faz cache dos pacotes NuGet entre
    execucoes.

    Gera migrations apenas (offline, sem banco). A APLICACAO das migrations
    acontece sozinha quando a API sobe (Database.Migrate() em Program.cs).

.EXAMPLE
    scripts/ef.ps1 migrations add AddAccessControl -o Persistence/Migrations

.EXAMPLE
    scripts/ef.ps1 migrations list

.EXAMPLE
    scripts/ef.ps1 migrations remove

.NOTES
    Nomes de migration nao podem conter espacos (limitacao do repasse de args).
    Usa $args (sem bloco param) para repassar flags como -o sem que o PowerShell
    tente liga-las a parametros do proprio script.
#>

$ErrorActionPreference = 'Stop'

if (-not $args -or $args.Count -eq 0) {
    Write-Host "Uso: scripts/ef.ps1 <args do dotnet ef>" -ForegroundColor Yellow
    Write-Host "Ex.: scripts/ef.ps1 migrations add AddAccessControl -o Persistence/Migrations"
    exit 1
}

$repo = (Resolve-Path "$PSScriptRoot/..").Path
$efVersion = '10.0.10'
$argLine = $args -join ' '

$inner = @"
set -e
export PATH="`$PATH:/root/.dotnet/tools"
dotnet tool install --global dotnet-ef --version $efVersion >/dev/null 2>&1 || true
dotnet restore src/Seed.Api/Seed.Api.csproj
dotnet ef $argLine --project src/Seed.Infrastructure --startup-project src/Seed.Api --no-color
"@

# O here-string herda CRLF no checkout Windows; o bash dentro do container
# quebra com o \r ao final de cada linha (ex.: `set -e\r`). Normaliza para LF.
$inner = $inner -replace "`r`n", "`n"

docker run --rm `
    -v "${repo}:/src" `
    -v "seed-nuget:/root/.nuget/packages" `
    -w /src/apps/api `
    mcr.microsoft.com/dotnet/sdk:10.0 `
    bash -c $inner
