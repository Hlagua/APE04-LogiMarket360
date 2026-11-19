# Bitácora de decisiones y cambios — LogiMarket 360

Registro vivo del proyecto. Cada cambio relevante agrega una entrada fechada (más reciente arriba).
Sirve de fuente para el informe final (que se elaborará cuando se apruebe el sistema).

## Estado actual

- Solución con 5 proyectos compilando sin errores ni advertencias; migración `Inicial`.
- 49 pruebas automatizadas en verde (`dotnet test`), que cubren los casos P01–P38 salvo P28 (ver decisión del 2026-11-19).
- Interfaz WinForms con 12 pestañas; prueba de humo: las 12 vistas cargan y el Mediator coordina sin errores.
- Git: commit inicial `9e6ad89`; a partir de aquí, un commit por cada iteración.
- Pendiente: recorrido manual de la interfaz por el usuario e informe PDF (al final).

## 2026-11-19 — Revisión externa del código

Otra herramienta revisó el proyecto. Se verificó cada hallazgo contra el código y la guía:

| Hallazgo | ¿Real? | Acción |
|---|---|---|
| Suscripción a `Ejecutor.Mensaje` sin desuscribir (FormPrincipal) | Sí. Una corrección anterior no se había aplicado (falló el script de edición) | Manejador con nombre y desuscripción en `FormClosed` |
| Vistas suscritas a `MetricasSesion.Cambiaron` sin desuscribir | Sí | `VistaBase.AlCambiarMetricas`: desuscribe en `Disposed` y verifica `IsDisposed` antes de `BeginInvoke` |
| Doble confirmación al cerrar sesión con carrito sin guardar | Sí | Bandera `_descarteConfirmado` para que `FormClosing` no repita la pregunta |
| Catálogo no se refrescaba tras crear/editar producto o crear kit | Sí (el Mediator no refresca al emisor, por diseño) | La vista emisora se refresca a sí misma antes de notificar |
| Cobro no se refrescaba tras confirmar | Sí | Se refresca; además el combo incluye pedidos PAGADO para demostrar P07 (repetir con la misma clave devuelve el resultado original sin cobrar) |
| Falta `appsettings.ejemplo.json` | Sí (la guía pide configuraciones de ejemplo) | Se creó y la app lee un `appsettings.json` opcional (rutas de base, salidas y logs) |
| El ZIP incluiría `GUIA_APE_04.docx` y `CLAUDE.md` | Sí | El script los excluye, junto con `.claude/` y un `appsettings.json` local |
| Un solo commit en Git | Sí, pero no se reescribe la historia | Se fabricarían fechas y fases falsas; la guía pide evolución verificable. Desde ahora, un commit por iteración |
| `CellFormatting` sin validar `RowIndex` | Defensa razonable | Se valida el índice |
| `ESTADO = 999` aceptado por el Interpreter | Sí (`Enum.TryParse` acepta números) | Se exige texto y `Enum.IsDefined`; prueba agregada en P26 |
| `MetricasSesion`: id 0, doble `lock`, aviso sin cambios | Sí | Un solo bloqueo; aviso solo si hubo cambio; eventos sin id se cuentan siempre; prueba en P15 |
| Formato decimal dependiente de la cultura en logs y guías | Sí | `Dinero.Formatear`; los comprobantes usan `AppendLine(InvariantCulture, …)` en vez de cambiar la cultura del hilo |
| `AgregarLinea` comparaba códigos sensible a mayúsculas | Sí (inconsistente con `ObtenerLinea`) | `OrdinalIgnoreCase` |
| Posible doble conteo de denegaciones (Facade + Proxy) | Sí, latente | La denegación se cuenta donde se origina (`ServicioAutorizacion` o `PagoAutorizadoProxy`); la Facade ya no cuenta. Prueba: 2 denegaciones = 2 en métricas |
| Registro DI sin uso de `FormularioPrincipalMediator` | Sí | Eliminado (se crea un mediador nuevo por sesión) |
| Comentario del Flyweight desactualizado | Sí | Corregido (memoria retenida con `GC.GetTotalMemory(true)`) |
| Cálculo `Subtotal - Descuento` en la vista de devoluciones | Sí (mínimo, pero es regla de negocio) | `PedidoResumenDto.ImporteReembolsable` lo trae del dominio |
| Prueba automática de P28 (Mediator) | Se descarta por la guía | El Mediator vive en WinForms y la guía fija que Tests referencia solo Domain, Application e Infrastructure. P28 se evidencia con la prueba de humo de la UI y en la sustentación |

## 2026-11-18 — Construcción inicial (F0–F6)

### Decisiones de diseño

| Tema | Decisión | Motivo |
|---|---|---|
| SDK | Se compila `net8.0`/`net8.0-windows` con el SDK 10 instalado | La guía exige .NET 8; el runtime 8 de escritorio está presente |
| Solución | `LogiMarket360.sln` (formato clásico, no .slnx) | Compatibilidad con Visual Studio 2022 |
| Pruebas | xUnit; cada prueba usa un archivo SQLite temporal nuevo | Aislar datos y probar transacciones/concurrencia reales |
| Estado devuelto | No hay estado DEVUELTO: `Pedido.Devuelto` (bandera) + `Devolucion` única por pedido | La guía define 6 estados; "ENTREGADO y no devuelto" |
| Confirmar desde BORRADOR | La Facade acepta BORRADOR o VALIDADO; si es BORRADOR registra VALIDADO y luego PAGADO en el historial | Flujo ágil desde el carrito sin saltarse el State |
| Edición | Solo en BORRADOR (un VALIDADO ya no se edita) | "No retroceder estados mediante el formulario" |
| Idempotencia | Clave por defecto `CONF-<id>`; índices únicos en `Pago.ClaveIdempotencia`, `Pago.PedidoId`, `Pago.Referencia`, `Pedido.ClaveIdempotencia` | Un reintento devuelve el resultado original sin volver a cobrar |
| Pagos rechazados | No se guardan en `Pago` (solo aprobados); el rechazo queda en Auditoría y bitácora | Permite reintentar con la misma clave tras un fallo |
| Concurrencia | `BEGIN IMMEDIATE` (transacción de Microsoft.Data.Sqlite) + token `Version` en Producto y Pedido | P36: máximo un cobro aprobado, sin sobreventa |
| Cobro en la transacción | Se cobra dentro de la transacción y antes de `SaveChanges`; si falla cualquier paso hay rollback total | P06/P20: sin persistencia parcial |
| Bitácora técnica | En memoria + archivo `logs/`, fuera de la transacción | Los LOG de INICIO/FIN/ERROR y la seguridad sobreviven a un rollback |
| Correlación | `AsyncLocal` (`ContextoCorrelacion`) iniciado por el Decorator | El LOG de la pasarela comparte la correlación del cobro |
| Outbox | Se procesa justo después del commit, sin propagar errores; candado global; cada evento se recarga | Evento pendiente si falla un observador; procesado una sola vez |
| Orden de observadores | Notificación → Auditoría → Métricas | Si falla el aviso no queda nada a medias; la auditoría es idempotente por evento |
| Devolución DEFECTO | También reintegra stock | P35 exige "un solo reintegro" para el caso de Ana por defecto |
| Permisos de devolución | ADMIN y VENTAS | La guía no lo fija; VENTAS gestiona pedidos y cobros/reembolsos |
| Ajuste de stock | Solo ADMIN, con movimiento AJUSTE | BODEGA prepara/despacha; el ajuste es administrativo |
| Kits | `KitComponente` con FK real a `Producto` o a `Kit` (CHECK: exactamente uno) | "PK y FK reales"; detección de ciclos con `ArbolCatalogo` antes de guardar |
| Precio de kit | Se calcula (Composite), no se almacena | KIT_OFICINA 50, KIT_TELETRABAJO 60 siempre coherentes |
| Interpreter CLIENTE | `CLIENTE = texto` es coincidencia parcial sin mayúsculas | Usabilidad ("CLIENTE = ana") |
| Formato monetario | Punto decimal y 2 decimales (cultura es-EC con separador "."), comprobantes en cultura invariante | Coincidir con las cifras de la guía (59.00) |
| Flyweight | Se mide memoria RETENIDA con `GC.GetTotalMemory(true)` en ambos escenarios equivalentes | No atribuir ahorros sin comparar condiciones iguales |
| Semilla | Clientes Ana/Luis/Carmen, usuarios demo con hash PBKDF2, productos P01–P05 y 2 kits; idempotente por clave natural | P01 y casos numéricos oficiales |
| UI | Vistas construidas en código (sin diseñador), `SplitContainer` con proporción aplicada al tener tamaño real | Evitar la excepción de `SplitterDistance` y escalar con la ventana |

### Problemas encontrados y corregidos

1. **Cultura decimal**: el comprobante mostraba `54,00` en un equipo con cultura es-EC → prueba P17 fallaba.
   Corrección: formato invariante en comprobantes y eventos; cultura de la app con punto decimal.
2. **`SplitterDistance` antes del tamaño real** lanzaba `InvalidOperationException` al crear la vista.
   Corrección: helper `Ui.Division` que fija la proporción al primer redimensionado válido.
3. **Procesador Outbox tras un fallo**: el contexto se limpiaba y los siguientes eventos quedaban desconectados.
   Corrección: recargar cada evento por id antes de procesarlo.
