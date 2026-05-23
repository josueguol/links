# Instrucciones para agentes de codigo

Este proyecto se trabaja con OpenCode siguiendo las reglas de `PROJECT_RULES.md`.

Antes de modificar codigo, el agente activo debe leer `PROJECT_RULES.md` y tratarlo como la fuente de verdad del proyecto.

## Contexto del proyecto

- Objetivo: comunidad de nicho (red social) con funcionalidades especificas.
- Alcance funcional inicial: auth/usuarios, feed/publicaciones, comentarios/reacciones. Nada fuera de ese alcance sin confirmacion.
- Backend: C# con .NET 10 (LTS) + ASP.NET Core Minimal APIs + Dapper.
- Validacion: FluentValidation. Errores esperados: `Result<T>` simple.
- Pruebas backend: xUnit.
- Base de datos: PostgreSQL externa (no en Docker local). Acceso solo via Dapper. Scripts SQL planos versionados.
- Frontend: TypeScript estricto + Web Components nativos (sin framework UI).
- Real-time: SignalR para feed, mensajeria y notificaciones.
- Auth propia: Argon2id, JWT + cookie refresh token rotation, TOTP MFA, rate limit via Redis.
- Empaquetado: Docker solo para la app.
- Arquitectura: monolito modular simple, un solo repo.

## Regla operativa principal

El agente debe hacer solo lo que se le pide.

Si una solicitud permite varias interpretaciones o requiere decidir arquitectura, contratos publicos, estructura de carpetas, base de datos, seguridad, librerias nuevas fuera del stack confirmado o flujos de producto, el agente debe detenerse y preguntar antes de escribir codigo.

Formato obligatorio para preguntas:

```md
Necesito aclarar esto antes de modificar código:

1. ...
2. ...
3. ...
```

## Limites importantes

- No inventar requisitos, endpoints, pantallas, entidades, modelos ni flujos fuera del alcance inicial.
- No crear schema, scripts SQL ni modelos de datos sin confirmar entidad, campos, relaciones e indices.
- No introducir EF Core, otro ORM, framework UI, CQRS/Mediator, AutoMapper, Clean Architecture completa, Generic Repository ni Unit of Work artificial sin aprobacion explicita.
- No introducir abstracciones, capas, patrones o librerias si no hay una necesidad clara.
- Preferir cambios pequenos, simples y faciles de revisar.
- Mantener funciones, clases y archivos de código productivo dentro de los límites definidos en `PROJECT_RULES.md`.
- En archivos de prueba (`src/Links.Tests/**`, `frontend/tests/**`) el límite de tamaño es una guía blanda. Dividir solo por cohesión semántica (por SUT o por feature), nunca por conteo de líneas. Ver sección "Archivos de prueba" en `PROJECT_RULES.md`.
- Si encuentra problemas no relacionados con la solicitud, reportarlos y preguntar antes de corregir.

## Flujo recomendado para el agente

1. Leer `PROJECT_RULES.md`.
2. Inspeccionar los archivos relevantes.
3. Si falta contexto o se requiere nueva estructura de carpetas/modulos, preguntar con el formato obligatorio.
4. Si el cambio es seguro y acotado, implementarlo de forma incremental.
5. Ejecutar validaciones disponibles segun el tipo de cambio.
6. Explicar brevemente que cambio y que no se pudo validar.

## Validaciones obligatorias (resumen)

Detalle completo en `PROJECT_RULES.md`. Mínimos por capa:

- Backend: `dotnet build Links.slnx` (0 warn / 0 err), `dotnet test Links.slnx` (100% verdes), `dotnet format Links.slnx --verify-no-changes`.
- Frontend: `npm --prefix frontend run lint`, `format:check`, `typecheck`, `build`.
- Cobertura mínima inicial: 40% (configurada en CI vía `coverlet.runsettings`).
- `TreatWarningsAsErrors=true` activo. `CS8602` y `CS8604` no pueden estar en `NoWarn`.

## Git y commits (resumen)

- Branch principal `main`. Cambios solo via PR.
- **Conventional Commits** obligatorio (`feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`), subject en minusculas, sin punto final, max 72 chars.
- Commits firmados (`git commit -S`) obligatorios para `main`.
- Sin secretos en commits ni historial.
- Sin `--no-verify` y sin `git push --force` sobre `main`.

## Hooks locales y CI (resumen)

- `lefthook.yml`: pre-commit (format/lint/gitleaks), commit-msg (commitlint), pre-push (build/test).
- Workflows GitHub: `ci.yml`, `codeql.yml`, `trivy.yml`, `gitleaks.yml`, `osv-scanner.yml`, `snyk.yml`, `socket.yml`, `pr-title.yml`, `dependency-review.yml`. Todos pasan por `harden-runner` en modo `audit`.
- Renovate gestiona dependencias (.NET, npm, GitHub Actions con `pinDigests: true`).
