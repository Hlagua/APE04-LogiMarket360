# Bitácora de decisiones y cambios — LogiMarket 360

Registro vivo del proyecto. Cada cambio relevante agrega una entrada fechada (más reciente arriba).
Sirve de fuente para el informe final (que se elaborará cuando se apruebe el sistema).

## Estado actual

- Solución con 5 proyectos compilando sin errores; migración `Inicial`.
- 49 pruebas automatizadas en verde (`dotnet test`), que cubren los casos P01–P38 salvo P28 (Mediator, solo visual).
- Interfaz WinForms con 12 pestañas; prueba de humo: las 12 vistas cargan y el Mediator coordina sin errores.
- Pendiente: recorrido manual de la interfaz por el usuario, historial Git por fases, informe PDF (al final).

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
