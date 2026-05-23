# Links

Proyecto en etapa inicial para crear una red social.

Stack confirmado:

- Backend: C# con .NET 10 (LTS) + Dapper.
- Base de datos: PostgreSQL externa (no se levanta en Docker local).
- Frontend: TypeScript + Web Components nativos (sin framework UI).
- Real-time: SignalR para feed, mensajeria y notificaciones.
- Auth propia: Argon2id, JWT + refresh token rotation via cookie httpOnly, TOTP MFA, rate limit via Redis.
- Empaquetado: Docker solo para la app.
- Arquitectura: monolito modular, un solo repositorio.

## Configuración local

### Requisitos

- .NET 10 SDK
- Node.js 20+
- PostgreSQL 16+ (instancia externa)

### Base de datos

Crea una base de datos PostgreSQL y ejecuta los scripts SQL en orden:

```sh
# Desde la raiz del proyecto
psql -U tu_usuario -d links < src/Links.Api/Modules/Auth/Scripts/001_create_users.sql
psql -U tu_usuario -d links < src/Links.Api/Modules/Auth/Scripts/002_create_refresh_tokens.sql
psql -U tu_usuario -d links < src/Links.Api/Modules/Auth/Scripts/003_create_email_verification_tokens.sql
psql -U tu_usuario -d links < src/Links.Api/Modules/Auth/Scripts/004_create_password_reset_tokens.sql
psql -U tu_usuario -d links < src/Links.Api/Modules/Auth/Scripts/005_create_mfa.sql
```

### Secretos de configuración

El proyecto requiere dos valores sensibles que **no** deben estar en el repo.

Usa `dotnet user-secrets` desde la carpeta `src/Links.Api`:

```sh
cd src/Links.Api

# Cadena de conexion a PostgreSQL
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=links;Username=tu_usuario;Password=tu_password"

# Secreto para firmar JWT (minimo 32 caracteres, usar un valor aleatorio seguro)
dotnet user-secrets set "Jwt:Secret" \
  "una-clave-secreta-muy-larga-de-al-menos-32-caracteres"
```

### Data Protection (para Docker / produccion)

La app usa ASP.NET Core Data Protection para encriptar secretos MFA.
En Docker, las llaves de encriptacion deben persistirse en un volumen,
de lo contrario los secretos MFA existentes se vuelven indescifrables al reiniciar.

Por defecto, las llaves se guardan en `./keys` (relativo al directorio de trabajo).
Puedes configurar otro path con:

```sh
dotnet user-secrets set "DataProtection:KeyDirectory" "/persistent/keys"
```

En Docker:

```docker
docker run -v /host/path/keys:/app/keys ...
```

Si `DataProtection:KeyDirectory` no esta configurado, se usa `./keys`.
Agrega `keys/` a `.gitignore` si ejecutas localmente.

Alternativa con variables de entorno:

```sh
export ConnectionStrings__Default="Host=localhost;..."
export Jwt__Secret="una-clave-secreta-muy-larga-de-al-menos-32-caracteres"
```

### Iniciar desarrollo

```sh
./dev.sh
```

Esto levanta backend en `http://localhost:5000` y frontend en `http://localhost:5173`.

## Hooks locales (lefthook)

Los hooks de Git validan format, lint, type-check, tests y secret scan antes de cada commit y push.

### Requisitos del sistema

- `dotnet` 10 SDK
- `node` 20+
- `lefthook` (instalar con `brew install lefthook`)
- `gitleaks` (instalar con `brew install gitleaks`)

### Instalacion

Desde la raiz del repo:

```sh
# 1. Dependencias del root (commitlint)
npm install

# 2. Dependencias del frontend (eslint, prettier, vite, etc.)
npm --prefix frontend install

# 3. Activar los hooks de git
lefthook install
```

A partir de aqui:

- `git commit` corre `dotnet format --verify-no-changes`, `eslint`, `prettier --check`, `tsc --noEmit` y `gitleaks` sobre los archivos staged.
- El mensaje del commit pasa por `commitlint` (Conventional Commits estricto).
- `git push` corre `dotnet build`, `dotnet test` y `npm --prefix frontend run build`.

No uses `--no-verify` para saltarlos. Si un hook falla, corrige el problema antes de commitear.

### Comandos manuales utiles

```sh
# Backend
dotnet format Links.slnx
dotnet build Links.slnx
dotnet test Links.slnx
dotnet test Links.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Frontend
npm --prefix frontend run format       # autoformat
npm --prefix frontend run lint:fix
npm --prefix frontend run typecheck
npm --prefix frontend run build

# Hooks (correr manual sin commit)
lefthook run pre-commit
lefthook run pre-push
```

## Uso con OpenCode

1. Inicia OpenCode desde la raiz del proyecto:

```sh
cd /Users/josuegolivares/desarrollo/csharp/links
opencode
```

2. OpenCode cargara:

- `AGENTS.md` como reglas del proyecto.
- `opencode.json` como configuracion del proyecto.
- `PROJECT_RULES.md` desde el campo `instructions`.
- `.opencode/skills/project-rules/SKILL.md` como skill local.
- `.opencode/agents/*.md` como agentes de proyecto.

3. Modos y agentes:

- Usa los modos por defecto de OpenCode:
  - `plan` (default al iniciar): analiza y propone planes sin modificar archivos. Las reglas del proyecto (`AGENTS.md` + `PROJECT_RULES.md`) se cargan automaticamente.
  - `build`: implementa cambios pequenos con aprobacion para editar y ejecutar comandos. Las reglas de codificacion aplican aqui.
- Subagente local disponible:
  - `project-reviewer`: invocable desde el agente principal para revisar cambios sin editar archivos.

## Archivos de configuracion

- `AGENTS.md`: instrucciones de alto nivel para el agente.
- `PROJECT_RULES.md`: fuente de verdad con reglas obligatorias.
- `opencode.json`: configuracion de OpenCode (permisos, instrucciones, skills).
- `.opencode/skills/project-rules/SKILL.md`: skill local con reglas operativas.
- `.opencode/agents/*.md`: subagentes del proyecto (solo `project-reviewer`).

## Seguridad

No guardes llaves de API ni secretos en el repositorio.

Usa variables de entorno locales o `dotnet user-secrets` para credenciales.
