## Resumen

<!-- Que cambia este PR y por que. 1-3 oraciones. -->

## Tipo

<!-- Marcar uno -->
- [ ] feat
- [ ] fix
- [ ] refactor
- [ ] docs
- [ ] test
- [ ] chore / build / ci

## Alcance

- Modulos tocados:
- Archivos clave:

## Checklist

- [ ] El cambio cae dentro del alcance funcional inicial (auth, feed, comentarios/reacciones) o tiene aprobacion previa.
- [ ] No introduzco librerias fuera del stack confirmado sin justificacion en el PR.
- [ ] Metodos <= 35 lineas, clases <= 250 lineas, archivos <= 500 lineas.
- [ ] No mezclo HTTP con SQL ni reglas de negocio con persistencia.
- [ ] DTOs en limites de API.
- [ ] Pruebas xUnit agregadas o actualizadas cuando cambia logica de negocio o contratos.
- [ ] Sin secretos en el codigo. Sin logs con tokens, passwords o PII.
- [ ] `dotnet format` y `eslint`/`prettier` pasan localmente.
- [ ] CI verde.

## Riesgos / impacto

<!-- Que puede romper. Mitigaciones. -->

## Notas para el revisor

<!-- Atajos, contexto extra, decisiones discutibles. -->
