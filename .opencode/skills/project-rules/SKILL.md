---
name: project-rules
description: Reglas obligatorias para trabajar en este proyecto de red social. Usar antes de analizar, modificar o revisar codigo.
compatibility: opencode
metadata:
  project: links
---

# Project Rules

Usa esta skill antes de analizar, crear, modificar o revisar codigo en este proyecto.

## Fuente de verdad

Lee `PROJECT_RULES.md` antes de escribir codigo. Ese archivo tiene prioridad sobre convenciones genericas.

## Alcance funcional inicial

- Auth y usuarios.
- Feed / publicaciones.
- Comentarios / reacciones.

Nada fuera de ese alcance sin confirmacion previa.

## Comportamiento obligatorio

- Haz unicamente lo solicitado.
- No inventes requisitos, arquitectura, modelos, librerias, endpoints, pantallas, entidades ni flujos.
- Si falta contexto o hay varias soluciones posibles, pregunta antes de modificar codigo.
- Si necesitas crear nueva estructura de carpetas o modulos, proponla antes de modificar archivos.
- Mantén los cambios pequenos, simples, funcionales y faciles de revisar.

## Stack confirmado

- Backend: C# .NET 10 (LTS) + ASP.NET Core Minimal APIs + Dapper.
- Validacion: FluentValidation. Errores esperados: `Result<T>` simple.
- Pruebas backend: xUnit.
- Base de datos: PostgreSQL externa (no en Docker local). Acceso solo via Dapper.
- Frontend: TypeScript estricto + Web Components nativos (sin framework UI).
- Real-time: SignalR.
- Auth propia: Argon2id, JWT + cookie refresh token rotation, TOTP MFA, rate limit via Redis.
- Docker solo para la app.
- Monolito modular simple, un solo repo.

## Separacion de responsabilidades

- Endpoints (Minimal APIs): HTTP, binding, validacion superficial, llamadas a services. Sin SQL, sin logica de negocio.
- Validators (FluentValidation): reglas de validacion de request.
- Application Services: casos de uso. Sin dependencia de detalles HTTP.
- Repositories: SQL con Dapper. Sin reglas de negocio.
- DTOs en limites de API, SignalR y persistencia.
- Hubs SignalR: realtime. Sin logica de negocio pesada.

## Patrones prohibidos sin aprobacion

- Generic Repository.
- Unit of Work artificial.
- CQRS / Mediator.
- AutoMapper.
- Clean Architecture completa.
- Event sourcing.
- Framework UI frontend.
- EF Core u otro ORM.
- Librerias transversales nuevas.

## Restricciones criticas

- No crear tablas, scripts SQL ni modelos de datos sin confirmar entidad, campos, relaciones e indices.
- No introducir librerias nuevas fuera del stack confirmado sin aprobacion.
- No crear abstracciones o capas si no existe una necesidad clara.
- No modificar archivos no relacionados con la solicitud.
- Si encuentras problemas no relacionados, reportar y preguntar antes de corregir.

## Validaciones obligatorias

- Backend: `dotnet build Links.slnx` (0 warn / 0 err), `dotnet test Links.slnx`, `dotnet format Links.slnx --verify-no-changes`.
- Frontend: `npm --prefix frontend run lint`, `format:check`, `typecheck`, `build`.
- Cobertura minima: 40% (configurada en CI con `coverlet.runsettings`).
- `TreatWarningsAsErrors=true` activo. `CS8602` y `CS8604` no estan en `NoWarn`.

## Git y commits

- Conventional Commits estricto (`feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert`).
- Subject minusculas, sin punto final, max 72 chars.
- Commits firmados (`-S`) para `main`. Sin `--no-verify`. Sin `--force` sobre `main`.

## Hooks y CI

- `lefthook.yml`: pre-commit (format/lint/gitleaks), commit-msg (commitlint), pre-push (build/test).
- Workflows: `ci`, `codeql`, `trivy`, `gitleaks`, `osv-scanner`, `snyk`, `socket`, `pr-title`, `dependency-review`. Trivy bloquea HIGH/CRITICAL.

## Formato de pregunta obligatorio

```md
Necesito aclarar esto antes de modificar código:

1. ...
2. ...
3. ...
```
