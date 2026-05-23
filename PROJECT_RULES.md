# PROJECT_RULES.md

## Objetivo

Este archivo define las reglas que el agente OpenCode debe seguir al trabajar en este proyecto.

El agente debe crear código pequeño, simple, funcional, mantenible y fácil de revisar.
No debe inventar requisitos, arquitectura, modelos, librerías, endpoints, pantallas, entidades ni flujos que no hayan sido solicitados explícitamente.

---

## Alcance funcional inicial

El proyecto es una comunidad de nicho con funcionalidades específicas.

El desarrollo inicial debe enfocarse en:

1. Auth y usuarios.
2. Feed / publicaciones.
3. Comentarios / reacciones.

No crear módulos, pantallas, endpoints, entidades ni flujos fuera de ese alcance sin confirmación previa.

---

## Stack confirmado

- Backend: C# con .NET 10 (LTS).
- Web framework: ASP.NET Core Minimal APIs.
- Acceso a datos: Dapper.
- Validación: FluentValidation.
- Pruebas backend: xUnit.
- Migraciones: scripts SQL planos versionados (sin EF Migrations).
- Base de datos relacional: PostgreSQL (hosteada fuera del repo, no en Docker local).
- Frontend: TypeScript estricto + Web Components nativos (sin framework UI).
- Real-time: SignalR para feed, mensajería y notificaciones.
- Auth: propia, basada en ASP.NET Core sin Identity completo.
  - Hash de password: Argon2id (recomendación OWASP).
  - Tokens: JWT + refresh token rotation entregado en cookie httpOnly + Secure + SameSite=Strict.
  - MFA: TOTP desde el MVP.
  - Verificación de email + reset de password en MVP.
  - Rate limit de login y endpoints sensibles vía Redis.
- Empaquetado: Docker solo para la app (la base de datos no entra en compose).
- Arquitectura: monolito modular simple, un solo repositorio.

Las versiones de librerías deben ser estables y actuales al momento de instalarse.

---

## Arquitectura

La arquitectura será un monolito modular simple.

Reglas:

- Mantener módulos por funcionalidad cuando exista una necesidad clara.
- No aplicar Clean Architecture completa sin aprobación explícita.
- No crear capas genéricas o abstractas por anticipado.
- Preferir estructura simple y explícita.
- Cada módulo debe contener solo lo necesario para su responsabilidad.

---

## Principios obligatorios

El agente debe respetar siempre:

- Simplicidad antes que complejidad.
- Código funcional antes que código "elegante".
- SOLID aplicado con criterio, sin sobreingeniería.
- Responsabilidad única por clase, función, módulo y archivo.
- Cambios pequeños, incrementales y fáciles de revisar.
- Código explícito, legible y predecible.
- No introducir abstracciones si no existe una necesidad clara.
- No crear infraestructura, patrones o capas innecesarias.
- No modificar archivos no relacionados con la solicitud.

---

## Regla principal de comportamiento

El agente debe hacer únicamente lo que se le pide.

Si la solicitud no es clara, está incompleta o permite múltiples interpretaciones, debe detenerse y preguntar antes de escribir código.

No debe asumir.

No debe completar vacíos inventando lógica.

No debe crear código "probable" sin confirmación.

---

## Estilo de trabajo del agente

El agente debe trabajar de forma equilibrada:

- Implementar cambios claros, pequeños y acotados.
- Preguntar antes de modificar arquitectura, contratos públicos, estructura de carpetas, base de datos, seguridad o flujos de producto.
- Si necesita crear una nueva estructura de carpetas o módulos, debe proponerla antes de modificar archivos.

Formato obligatorio:

```md
Necesito aclarar esto antes de modificar código:

1. ...
2. ...
3. ...
```

---

## Cuándo debe preguntar antes de actuar

El agente debe preguntar antes de escribir código cuando:

- Falte contexto técnico.
- No esté claro el flujo esperado.
- No se especifique el archivo a modificar.
- No se conozca el contrato de entrada o salida.
- No esté claro el modelo de datos.
- Existan varias soluciones posibles.
- La solicitud pueda afectar arquitectura, seguridad, base de datos o contratos públicos.
- Se requiera elegir una librería nueva fuera del stack confirmado.
- Se requiera crear una abstracción nueva.
- Se requiera modificar comportamiento existente sin pruebas claras.
- Se requiera crear o modificar estructura de carpetas o módulos.

---

## Separación de responsabilidades

La separación debe ser fuerte:

- **Endpoints (Minimal APIs)**: HTTP, binding, validación superficial, llamadas a application services, traducción de resultados a respuestas HTTP.
- **Validators (FluentValidation)**: reglas de validación de request.
- **Application Services**: casos de uso y coordinación. Sin dependencia de detalles HTTP.
- **Domain / Business logic**: reglas centrales del negocio cuando existan.
- **Repositories**: SQL y persistencia con Dapper.
- **DTOs**: contratos de entrada/salida o transporte de datos.
- **Hubs SignalR**: comunicación realtime. Sin lógica de negocio pesada.

No mezclar:

- HTTP con SQL.
- SQL con reglas de negocio.
- Validación de request con persistencia.
- Render frontend con acceso directo a API y estado global en el mismo bloque.

---

## Reglas para backend (.NET 10 + Minimal APIs + Dapper)

Reglas:

- Usar .NET 10 (LTS) y la última versión estable de C# compatible.
- ASP.NET Core Minimal APIs como mecanismo principal de endpoints.
- Nullable reference types habilitados.
- Usar `async`/`await` correctamente.
- No bloquear hilos con `.Result`, `.Wait()` o llamadas síncronas innecesarias.
- No exponer entidades de persistencia directamente como contratos públicos.
- Usar DTOs en los límites de la API.
- Validar entradas con FluentValidation cuando se implementen endpoints o casos de uso.
- Manejar errores de forma explícita.
- No capturar excepciones sin acción clara. Prohibido `catch` vacío.
- Endpoints no deben contener lógica de negocio.
- Endpoints no deben ejecutar SQL.
- Repositories concretos son los únicos responsables de SQL con Dapper.
- Repositories no deben decidir reglas de negocio.
- Application services contienen casos de uso y reglas de aplicación.
- Las reglas de negocio no deben depender directamente de infraestructura.

---

## Acceso a datos

Usar SQL directamente en repositories concretos con Dapper.

Permitido:

- Repositories concretos por módulo o agregado.
- Queries SQL explícitas, parametrizadas y legibles.
- DTOs internos para lectura cuando ayuden a mapear resultados.

Prohibido:

- Generic Repository.
- Unit of Work artificial.
- SQL concatenado con input del usuario.
- Exponer entidades de persistencia como contratos públicos.
- Crear tablas, índices o scripts SQL sin confirmar entidad, campos, relaciones e índices.

---

## Validación y errores

Usar FluentValidation para validación de entrada cuando se implementen endpoints o casos de uso.

Usar `Result<T>` simple para errores esperados de negocio.

Reglas:

- No usar excepciones para flujo normal de negocio.
- Las excepciones deben reservarse para errores inesperados.
- Los errores esperados deben expresarse con `Result<T>` o equivalente simple.
- Los endpoints traducen resultados a respuestas HTTP.
- Los application services no deben depender de detalles HTTP.

---

## Patrones permitidos

Permitidos cuando exista necesidad clara:

- DTOs en límites de API, SignalR y persistencia.
- Options Pattern para configuración tipada.
- Repository concreto para Dapper.
- Application Services para casos de uso.
- Result pattern simple para errores esperados.
- Domain Events solo si hay efectos internos reales entre módulos.

Requieren aprobación explícita:

- CQRS.
- Mediator.
- Domain Events si implican infraestructura, persistencia, colas o comportamiento transversal.

---

## Patrones y enfoques prohibidos sin aprobación

No introducir sin aprobación explícita:

- Generic Repository.
- Unit of Work artificial.
- CQRS / Mediator.
- AutoMapper.
- Clean Architecture completa.
- Event sourcing.
- Framework UI frontend.
- EF Core u otro ORM.
- Librerías transversales nuevas.

---

## Reglas para frontend (TypeScript + Web Components)

Reglas:

- TypeScript estricto (`strict: true`).
- Sin framework UI (sin React, Vue, Svelte, Angular, etc.).
- Web Components nativos (Custom Elements + Shadow DOM cuando ayude a encapsular estilos).
- Bundler / dev server: a confirmar antes de instalar (probable Vite). No instalar sin aprobación.
- Separar estrictamente API, estado, render y estilos.
- No mezclar llamadas HTTP directamente dentro de render.
- No introducir estado global sin necesidad clara.
- Componentes pequeños, una responsabilidad cada uno.
- Nombres claros para componentes, funciones y archivos.
- Evitar librerías UI que arrastren dependencias pesadas.
- Tipos compartidos con el backend definidos manualmente o generados desde un contrato confirmado.

---

## Reglas para base de datos (PostgreSQL)

Hosting: PostgreSQL externo (no se levanta en Docker local).

Reglas:

- Versión: a confirmar al primer contacto con la instancia (preferir 16+).
- Acceso desde el backend solo vía Dapper.
- Esquema definido en scripts SQL versionados dentro del repo.
- No crear tablas, vistas, índices o tipos sin definir antes el caso de uso concreto.
- Usar `uuid` para IDs públicos cuando aplique, `bigserial` / `identity` para PKs internos cuando convenga.
- Nombrar tablas en `snake_case` plural y columnas en `snake_case`.
- Índices solo cuando exista una consulta real que los justifique.
- Migraciones idempotentes cuando sea razonable.
- No mezclar datos de prueba con scripts de esquema.

Antes de crear cualquier tabla o script, confirmar con el usuario:

- Entidad concreta.
- Campos exactos.
- Relaciones.
- Índices necesarios.

---

## Reglas de seguridad y autenticación

Las reglas de seguridad deben ser estrictas desde el inicio.

Auth propia. Reglas obligatorias:

- Hash de password: Argon2id con parámetros recomendados por OWASP (memoria, iteraciones, paralelismo a confirmar antes de implementar). Librería sugerida: `Konscious.Security.Cryptography.Argon2` (validar mantenimiento antes de fijar).
- Tokens:
  - JWT de corta duración (access token) firmados con clave asimétrica o secreto fuerte (a confirmar).
  - Refresh token rotativo, persistido y revocable, entregado vía cookie httpOnly + Secure + SameSite=Strict.
  - Cada uso de refresh genera token nuevo e invalida el anterior (rotation).
  - Detección de reuso de refresh token revoca toda la sesión.
- MFA: TOTP (RFC 6238) desde MVP. Backup codes de un solo uso.
- Email:
  - Verificación de email obligatoria antes de habilitar cuenta.
  - Reset de password vía token de un solo uso, expira en minutos.
- Rate limit vía Redis (sliding window o token bucket) en:
  - Login.
  - Registro.
  - Reset de password.
  - Verificación TOTP.
  - Endpoints de envío de mensajes.
- No registrar passwords, tokens, secretos ni códigos MFA en logs.
- Secretos solo en variables de entorno o secret manager. Nunca en el repo.
- Validar siempre entrada del usuario en endpoints sensibles.
- Aplicar headers de seguridad estándar (CSP, HSTS, X-Content-Type-Options, Referrer-Policy) cuando se configure HTTP público.
- CORS configurado explícitamente, no abierto.

Confirmar parámetros concretos antes de implementar auth, JWT, Argon2id, TOTP o refresh token rotation.

---

## Reglas de real-time (SignalR)

- SignalR para feed en vivo, mensajería y notificaciones.
- Hubs pequeños, una responsabilidad por hub.
- Autenticación obligatoria en todos los hubs.
- No exponer entidades de persistencia por SignalR. Usar DTOs.
- Sin lógica de negocio pesada dentro de hubs.
- Backplane (Redis SignalR backplane) solo si hay más de una instancia. No instalar antes de necesitarlo.
- Reconexión automática manejada por el cliente.

---

## Pruebas

Usar xUnit para backend.

Las pruebas son obligatorias cuando:

- Se agrega lógica de negocio.
- Se corrige un bug.
- Se cambia comportamiento observable.
- Se agregan validaciones.
- Se modifica un application service.
- Se cambia un contrato público.

Reglas:

- Las pruebas deben ser pequeñas y enfocadas.
- No probar detalles internos innecesarios.
- No agregar framework de testing nuevo sin aprobación.
- Framework de testing frontend pendiente de confirmar antes de instalar.

---

## Límites de tamaño de código

### Métodos / funciones

- Máximo recomendado: 25 líneas.
- Máximo permitido: 35 líneas.
- Máximo 4 parámetros. Si se necesitan más, usar un objeto / DTO.
- Máximo 3 niveles de indentación.
- Si supera el límite, dividir por responsabilidad.

### Clases

- Máximo recomendado: 150 líneas.
- Máximo permitido: 250 líneas.
- Máximo recomendado de métodos públicos: 7.
- Máximo recomendado de métodos totales: 12.
- Si una clase crece demasiado, dividir por responsabilidad.

### Archivos

Aplica a **código productivo** (`src/Links.Api/**`, `frontend/src/**`, repositories, services, endpoints, validators, DTOs, hubs, componentes):

- Máximo recomendado: 300 líneas.
- Máximo permitido: 500 líneas.
- Si un archivo de código productivo supera el límite, proponer una división antes de modificarlo.

### Archivos de prueba

Aplica a `src/Links.Tests/**` y `frontend/tests/**` (o equivalentes).

Los límites de archivos productivos **no aplican como límite duro** a archivos de pruebas. En tests, el tamaño es una guía blanda, no un gate.

Reglas para tests:

- No dividir un archivo de pruebas solo por conteo de líneas.
- Dividir solo cuando exista un **eje semántico claro**:
  - Un archivo de tests por SUT (clase bajo prueba). Si producción se divide en `AuthService` y `MfaService`, los tests deben reflejar esa separación: `AuthServiceTests` y `MfaServiceTests`.
  - O por feature/caso de uso cuando el SUT es grande y los grupos de pruebas son independientes (`AuthService_LoginTests`, `AuthService_RegisterTests`).
- Extraer **fixtures, fakes, builders y helpers** a archivos compartidos (`AuthTestFixture.cs`, `FakeUserRepository.cs`, `UserBuilder.cs`) cuando reduzcan ruido real, no para inflar/desinflar conteos.
- Señal blanda sugerida: si un archivo de tests supera ~800 líneas o mezcla más de un SUT, evaluar división por los criterios anteriores. Si no hay eje semántico claro, dejarlo y documentar el motivo en el PR.
- La cohesión por SUT pesa más que el conteo. Un `AuthServiceTests.cs` de 1000 líneas, todas sobre `AuthService`, es legítimo.

Prohibido en tests:

- Dividir por número arbitrario (ej. "parte 1 / parte 2") sin criterio semántico.
- Romper cohesión de un mismo SUT entre varios archivos sin razón clara.
- Duplicar fixtures/fakes entre archivos para evitar el split correcto.

---

## Reglas SOLID

### Single Responsibility Principle

Cada clase, función o módulo debe tener una sola razón para cambiar.

No mezclar:

- Validación.
- Persistencia.
- Reglas de negocio.
- Transformación de datos.
- Lógica de presentación.
- Acceso a infraestructura.

### Open/Closed Principle

Extender comportamiento sin modificar código existente cuando tenga sentido.

No crear interfaces o abstracciones si solo existe una implementación y no hay necesidad real.

### Liskov Substitution Principle

No crear jerarquías artificiales.

No usar herencia si composición resuelve mejor el problema.

### Interface Segregation Principle

Interfaces pequeñas y enfocadas.

No crear interfaces genéricas con métodos que no todos los consumidores usan.

### Dependency Inversion Principle

La lógica de negocio no debe depender directamente de infraestructura.

Usar abstracciones solo cuando ayuden a desacoplar código real.

---

## Dependencias

El agente solo puede agregar librerías dentro del stack confirmado.

Antes de agregar cualquier librería debe indicar:

```md
Librería propuesta:
Motivo:
Alternativas:
Impacto:
¿Requiere aprobación?
```

Reglas:

- No instalar librerías nuevas sin aprobación si afectan arquitectura, seguridad, build, testing o despliegue.
- No introducir librerías por conveniencia menor.
- Preferir APIs estándar de .NET cuando sean suficientes.
- No agregar dependencias sin aprobación cuando aumenten complejidad, introduzcan runtime nuevo, cambien la forma de compilar/probar/desplegar o tengan impacto de seguridad.

---

## Prohibiciones generales

El agente no debe:

- Inventar requisitos, entidades, endpoints, pantallas, reglas de negocio, estructura de carpetas o flujos no solicitados.
- Agregar librerías sin necesidad.
- Hacer refactors grandes sin pedir permiso.
- Cambiar arquitectura sin pedir permiso.
- Crear código "por si acaso".
- Crear abstracciones prematuras.
- Modificar muchos archivos para resolver algo pequeño.
- Escribir código viejo o APIs obsoletas.
- Ignorar errores de compilación.
- Ignorar pruebas fallidas.
- Eliminar código sin explicar por qué.
- Cambiar nombres públicos sin confirmar impacto.
- Introducir EF Core, otro ORM o framework UI sin aprobación explícita.

---

## Problemas no relacionados

Si el agente encuentra problemas no relacionados con la solicitud:

- Debe reportarlos.
- Debe preguntar antes de corregirlos.
- No debe incluirlos en el mismo cambio sin aprobación.

---

## Formato de respuesta esperado

Cuando la solicitud sea clara, el agente debe responder así:

```md
## Entendido

Haré exactamente esto:

1. ...
2. ...

## Archivos a modificar

- ...

## Cambios

...
```

Cuando la solicitud sea ambigua:

```md
## Necesito aclaración

Antes de escribir código necesito confirmar:

1. ...
2. ...
3. ...
```

Cuando proponga una solución:

```md
## Propuesta

...

## Por qué

...

## Impacto

...

## Riesgos

...
```

---

## Reglas para cambios de código

Antes de modificar código, el agente debe identificar:

- Qué archivo se modifica.
- Por qué se modifica.
- Qué comportamiento cambia.
- Qué comportamiento no cambia.
- Qué pruebas deben ejecutarse.

No debe tocar archivos fuera del alcance.

---

## Reglas de calidad

Antes de terminar, el agente debe validar:

- El código compila con `TreatWarningsAsErrors=true`.
- No hay imports sin usar.
- No hay código muerto.
- No hay duplicación innecesaria.
- Los métodos respetan el límite de líneas.
- Las clases de código productivo respetan el límite de tamaño. En tests, el tamaño es guía blanda; la cohesión por SUT manda.
- La solución hace solo lo solicitado.
- No se introdujeron dependencias sin aprobación.
- No se asumió comportamiento no confirmado.

---

## Git y commits

- Branch principal: `main`. Los cambios entran solo via Pull Request.
- Mensajes de commit obligatorios en formato **Conventional Commits** (`feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`).
- Subject en minúsculas, sin punto final, máximo 72 caracteres.
- Body (cuando exista) precedido por línea en blanco.
- Commits firmados (`git commit -S`) obligatorios para `main`.
- No incluir secretos, tokens, passwords o PII en commits ni en historial.
- No usar `git push --force` sobre `main`. No usar `--no-verify` para saltar hooks.
- Reglas del repo (`renovate.json`, workflows, `Directory.Build.props`, `lefthook.yml`) solo cambian en PR explícito, no como cambios "de paso".

---

## Validaciones obligatorias

Todo cambio debe pasar estas validaciones antes de subirse:

| Capa | Comando | Resultado esperado |
|---|---|---|
| Backend build | `dotnet build Links.slnx` | `0 Advertencia(s)`, `0 Errores` |
| Backend tests | `dotnet test Links.slnx` | 100% verdes |
| Cobertura | `dotnet test ... --collect:"XPlat Code Coverage" --settings coverlet.runsettings` | ≥ 40% (umbral inicial) |
| Backend format | `dotnet format Links.slnx --verify-no-changes` | sin diff |
| Frontend lint | `npm --prefix frontend run lint` | 0 errores |
| Frontend format | `npm --prefix frontend run format:check` | sin diff |
| Frontend typecheck | `npm --prefix frontend run typecheck` | 0 errores |
| Frontend build | `npm --prefix frontend run build` | OK |

`TreatWarningsAsErrors=true` y `EnforceCodeStyleInBuild=true` están activos en `Directory.Build.props`. No relajar estas banderas para resolver warnings; corregir el código o, si el ruido no aporta señal, agregar el ID puntual a `NoWarn` con justificación. `CS8602` y `CS8604` (nullability) **no** deben aparecer en `NoWarn`.

---

## Lint y format — Backend (.NET 10)

Analyzers activos vía `Directory.Build.props`:

- `StyleCop.Analyzers` (estilo).
- `Roslynator.Analyzers` (refactor + smells).
- `SonarAnalyzer.CSharp` (bugs + code smells).
- `SecurityCodeScan.VS2019` (SAST: SQL injection, XSS, weak crypto, hardcoded secrets).
- `Microsoft.CodeAnalysis.NetAnalyzers` (built-in, `AnalysisMode=Recommended`).

Reglas operativas:

- `dotnet format Links.slnx` antes de cada commit.
- Nullability obligatoria (`Nullable=enable`). Sin `!` ni `?` "por si acaso"; usar `Assert.NotNull` + bang solo donde el dominio garantice no-null (típico en tests cuando el path de éxito ya fue validado por `Assert.True(result.IsSuccess)`).
- Sin `catch` vacío. Sin `.Result` / `.Wait()` síncronos.
- Sin SQL concatenado: solo queries parametrizadas con Dapper.

---

## Lint y format — Frontend (TypeScript + Web Components)

Herramientas instaladas en `frontend/`:

- **ESLint** flat config (`eslint.config.js`) con:
  - `@eslint/js` recommended.
  - `typescript-eslint` strict + stylistic, **type-checked** solo sobre `src/**/*.{ts,tsx}`.
  - `eslint-plugin-security`.
  - `eslint-plugin-no-unsanitized` (bloquea `innerHTML` / `outerHTML` sin sanitizar — crítico para Web Components).
- **Prettier** (`.prettierrc.json`).
- **TypeScript** estricto (`tsconfig.json` con `strict`, `noUnusedLocals`, `noUnusedParameters`, `exactOptionalPropertyTypes`, `noUncheckedIndexedAccess`).

Reglas operativas:

- Nada de `innerHTML` con datos del usuario. Usar `textContent` o `DOMPurify` (si se aprueba dependencia).
- Sin `eval`, `new Function`, `setTimeout(string, ...)`.
- Promesas no pueden quedar flotando (`no-floating-promises`).
- `console.log` desaconsejado; permitidos `console.warn` y `console.error` cuando aporten señal.

---

## Hooks locales (lefthook)

`lefthook.yml` ejecuta validaciones antes de cada `git commit`, `git commit -m` y `git push`:

- **pre-commit**: `dotnet format --verify-no-changes`, `eslint`, `prettier --check`, `tsc --noEmit`, `gitleaks protect --staged`.
- **commit-msg**: `commitlint` (Conventional Commits).
- **pre-push**: `dotnet build`, `dotnet test`, `npm --prefix frontend run build`.

Binarios requeridos localmente: `dotnet`, `node`, `npm`, `lefthook`, `gitleaks`. Sin estos binarios, no se debe saltar hooks con `--no-verify`.

---

## CI y security scans (GitHub Actions)

Workflows en `.github/workflows/`. Todos pasan por `step-security/harden-runner` en modo `audit`.

| Workflow | Trigger | Verificación |
|---|---|---|
| `ci.yml` | push/PR a `main` | build + tests + cobertura ≥ 40% + lint frontend + commitlint + `dotnet list package --vulnerable` |
| `codeql.yml` | push/PR/cron | CodeQL C# + JS/TS, queries security-extended + quality |
| `trivy.yml` | push/PR/cron | scan fs + imagen Docker, fail en `HIGH,CRITICAL` |
| `gitleaks.yml` | push/PR/cron | scan secretos en historial |
| `osv-scanner.yml` | push/PR/cron | OSV-Scanner sobre dependencias |
| `snyk.yml` | push/PR/cron | Snyk .NET + Node (requiere `SNYK_TOKEN`) |
| `socket.yml` | PR | Socket dependency review (requiere `SOCKET_SECURITY_API_KEY`) |
| `pr-title.yml` | PR | título en formato Conventional Commits |
| `dependency-review.yml` | PR | GitHub Dependency Review, fail HIGH, deny GPL/AGPL |

Renovate (vía GitHub App + `renovate.json`) abre PRs semanales agrupados para .NET y npm. Acciones de GitHub se actualizan con `pinDigests: true`.

Pasos obligatorios después de cualquier cambio de seguridad:

- Verificar que los SARIF de CodeQL, Trivy y Snyk siguen sin findings nuevos HIGH/CRITICAL.
- Revisar el comentario de PR de Socket cuando aplique.

---

## Comandos según el tipo de cambio

| Tipo de cambio | Comandos mínimos antes de commit |
|---|---|
| Cambio solo backend (lógica, repos, services) | `dotnet format Links.slnx --verify-no-changes`, `dotnet build Links.slnx`, `dotnet test Links.slnx` |
| Cambio solo frontend (`frontend/`) | `npm --prefix frontend run format:check`, `npm --prefix frontend run lint`, `npm --prefix frontend run typecheck`, `npm --prefix frontend run build` |
| Cambio en SQL (`Scripts/*.sql`) | revisar que el script sea parametrizable e idempotente; verificar ortografía de columnas contra el repository que las consume; sin tests automáticos disponibles, validar manualmente |
| Cambio en `Dockerfile` | `docker build -t links-api:dev -f Dockerfile .` localmente |
| Cambio en workflows (`.github/workflows/*.yml`) | revisar sintaxis con `actionlint` si está instalado; abrir PR aparte |
| Cambio en `Directory.Build.props`, `coverlet.runsettings`, analyzers | rebuild full backend + tests, confirmar 0 warnings |
| Cambio en `lefthook.yml` | ejecutar `lefthook run pre-commit` y `lefthook run pre-push` localmente |
| Cambio en `package.json` (frontend o root) | regenerar `package-lock.json` con `npm install`, confirmar `npm audit --audit-level=high` |
| Cambio en `*.csproj` o `Directory.Build.props` | `dotnet restore --force-evaluate` para refrescar `packages.lock.json`, commitear el lock |

Si el cambio cruza varias capas, ejecutar los comandos de cada capa afectada.

---

## Política de versiones

El agente debe usar versiones estables actuales de lenguajes, runtimes y librerías.

Reglas:

- .NET 10 (LTS) fijo para backend.
- Verificar la última versión estable de librerías antes de instalarlas.
- No usar versiones obsoletas.
- No usar APIs deprecadas si existe una alternativa moderna y estable.
- No fijar versiones antiguas sin justificación.
- Si una versión nueva puede romper compatibilidad, preguntar antes de actualizar.

---

## Regla final

Si el agente no está seguro, debe preguntar.

No debe actuar primero y justificar después.

La prioridad es:

1. Claridad.
2. Correctitud.
3. Simplicidad.
4. Mantenibilidad.
5. Velocidad.
