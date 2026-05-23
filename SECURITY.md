# Security Policy

## Supported Versions

Solo `main` recibe parches de seguridad. No hay releases publicadas aun.

## Reporting a Vulnerability

Reporta vulnerabilidades de forma privada vía **GitHub Private Vulnerability Reporting**:

1. Ir a `Security` → `Report a vulnerability`.
2. Describir el problema con detalle, pasos de reproduccion e impacto estimado.
3. No abrir issues publicas con detalles de vulnerabilidades.

Tiempo objetivo de respuesta: 72 horas habiles.

Si no es posible usar el reporte privado, contactar al mantenedor por correo (ver perfil de GitHub).

## Required Secrets (GitHub Actions)

Para que los workflows funcionen, configurar en `Settings → Secrets and variables → Actions`:

| Secret | Workflow | Notas |
|---|---|---|
| `SNYK_TOKEN` | `snyk.yml` | Token de cuenta Snyk free |
| `SOCKET_SECURITY_API_KEY` | `socket.yml` | API key de Socket Security (opcional si se usa la GitHub App) |

## Recommended Repo Settings

- Branch protection en `main`: PRs obligatorios, ≥1 reviewer, status checks requeridos, conversaciones resueltas, historial lineal, push directo bloqueado, force push bloqueado.
- Require **signed commits** en `main`.
- Habilitar **secret scanning** y **push protection**.
- Habilitar **Dependabot alerts** (las actualizaciones de version las maneja Renovate).
- Habilitar **private vulnerability reporting**.
- Habilitar **code scanning** (CodeQL via workflow).
- Action permissions: `Allow select actions`, requerir SHA pinning para acciones externas.

## Supply Chain

- Acciones de GitHub deberian fijarse por SHA. Inicialmente algunas estan por tag; Renovate convierte a SHA al primer ciclo (`pinDigests: true`).
- Lock files versionados (`packages.lock.json`, `package-lock.json`).
- Imagen Docker escaneada por Trivy (fail en HIGH/CRITICAL).
- Dependencias escaneadas por OSV-Scanner, Snyk y Socket.

## Local Hooks

`lefthook` corre validaciones antes de cada commit y push.
`gitleaks` debe estar instalado localmente para el hook de secretos.

Instalacion:

```sh
brew install lefthook gitleaks
lefthook install
```
