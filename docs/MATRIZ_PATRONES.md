# Matriz de los 23 patrones GoF (23/23)

| # | Patrón | Participantes principales | Archivo | Dónde actúa en ejecución | Prueba |
|---|---|---|---|---|---|
| 1 | Factory Method | `CreadorNotificador` (abstracto, `CrearNotificador()`), `CreadorNotificadorEmail`, `CreadorNotificadorSms`, `SelectorCanalNotificacion` | Application/Notificaciones/CreadoresNotificador.cs; Infrastructure/Notificaciones/Notificadores.cs | Observer de notificaciones al procesar el Outbox (pago, despacho, devolución) | P11 |
| 2 | Abstract Factory | `IFabricaEntrega`, `EntregaEstandarFactory`, `EntregaExpresFactory`, productos tarifa/plazo/guía, `ProveedorFabricasEntrega` | Application/Entrega/FabricasEntrega.cs | Valoración (tarifa) y despacho (plazo + guía) | P12 |
| 3 | Builder | `PedidoBuilder` | Domain/Pedidos/PedidoBuilder.cs | Creación del borrador en `EditorBorrador` | P13 |
| 4 | Prototype | `IPrototipo<T>`, `Pedido.Clonar()`, `PedidoDetalle.Clonar()` | Domain/Comun/IPrototipo.cs; Domain/Pedidos/Pedido.cs | "Duplicar como borrador" (`ServicioPedidos.ClonarComoBorradorAsync`) | P14 |
| 5 | Singleton | `MetricasSesion` (constructor privado, `MetricasSesion.Instancia`), registrado en DI como `IMetricasSesion` con esa misma instancia | Application/Metricas/MetricasSesion.cs; Infrastructure/RegistroDependencias.cs | Panel, Auditoría y observadores comparten la instancia | P15 |
| 6 | Adapter | `PasarelaLegadaSimulada.CobrarCentavos(long,string)` → `PasarelaLegadaAdapter : IPasarelaPago` (añade idempotencia por clave); alterno `PasarelaModernaSimulada` | Infrastructure/Pagos/*.cs | Cobro en la confirmación | P16, P38 |
| 7 | Bridge | `Comprobante` (`ComprobanteVenta`, `ComprobanteDevolucion`) × `ISalidaDocumento` (`SalidaVistaPrevia`, `SalidaArchivoTxt`) | Application/Reportes/Comprobantes.cs; WinForms/Infraestructura/SalidaVistaPrevia.cs; Infrastructure/Documentos/SalidaArchivoTxt.cs | Pestaña Reportes → Emitir comprobante | P17 |
| 8 | Composite | `IElementoCatalogo`, `ProductoSimple`, `KitCompuesto`, `ArbolCatalogo` | Domain/Catalogo/*.cs | Precios de kits, expansión a SKU, detección de ciclos | P18 |
| 9 | Decorator | `AuditoriaPagoDecorator` | Application/Pagos/AuditoriaPagoDecorator.cs | Rodea el cobro: INICIO → PASARELA → FIN/ERROR | P19 |
| 10 | Facade | `ConfirmacionPedidoFacade` | Application/Pedidos/ConfirmacionPedidoFacade.cs | Botón "Confirmar y cobrar" (vía Command) | P02–P07, P20 |
| 11 | Flyweight | `CatalogoVisualFlyweightFactory : IFabricaMetadatosSku`, `MetadatosSku`, `FilaCatalogoVisual`, `GeneradorVistaMasiva` | Application/Catalogo/CatalogoVisualFlyweight.cs | Reportes → Vista masiva (1.000 filas) | P21 |
| 12 | Proxy | `PagoAutorizadoProxy` | Application/Pagos/PagoAutorizadoProxy.cs | Antes del Decorator: CONSULTA/BODEGA no llegan a la pasarela | P22 |
| 13 | Chain of Responsibility | `EslabonValidacion`, `ClienteActivoValidador`, `LineasCantidadesValidador`, `StockDisponibleValidador`, `CondicionesConfirmacionValidador` | Application/Pedidos/CadenaValidacion.cs | Validación y confirmación | P05, P23 |
| 14 | Command | `IComando`, `ConfirmarPedidoCommand`, `CancelarPedidoCommand`, `DespacharPedidoCommand`, `EntregarPedidoCommand`, `ProcesarDevolucionCommand`, `InvocadorComandos` | Application/Comandos/Comandos.cs | Botones de Cobro, Pedidos, Despacho, Devoluciones | P08, P24 |
| 15 | Interpreter | `IExpresionFiltro`, expresiones terminales/no terminales, `AnalizadorFiltro` | Application/Reportes/InterpreteFiltro.cs | Reportes → filtro de pedidos | P25, P26 |
| 16 | Iterator | `IIteradorComponentes`, `IteradorComponentesFisicos` | Domain/Catalogo/IteradorComponentesFisicos.cs | Instantánea de líneas, stock por SKU, desglose | P27 |
| 17 | Mediator | `IMediadorVistas`, `FormularioPrincipalMediator`, colegas `VistaBase` | WinForms/Infraestructura/Mediador.cs | Refresco de catálogo, pedidos, inventario y métricas tras cada operación | P28 (prueba de humo UI) |
| 18 | Memento | `PedidoMemento`, `HistorialBorrador` (caretaker, 5 estados, incluye el carrito vacío), `EditorBorrador` | Domain/Pedidos/PedidoMemento.cs | Carrito → Deshacer/Rehacer | P29 |
| 19 | Observer | `PublicadorEventos`, `ObservadorNotificaciones`, `ObservadorAuditoria`, `ObservadorMetricas`, `ProcesadorOutbox` | Application/Eventos/*.cs | Tras confirmar la transacción (Outbox) | P30, P37 |
| 20 | State | `EstadoPedidoBase` y 6 estados concretos | Domain/Pedidos/Estados/EstadosPedido.cs | Toda transición del pedido | P08, P09, P31 |
| 21 | Strategy | `IPoliticaDescuento`, `SinDescuento`, `Vip10`, `Mayorista15`, `CalculadoraDescuento` | Domain/Promociones/PoliticasDescuento.cs | Valoración del pedido | P32 |
| 22 | Template Method | `ProcesarDevolucion.EjecutarAsync`, `ProcesarDevolucionDefecto`, `ProcesarDevolucionArrepentimiento` | Application/Devoluciones/ProcesarDevolucion.cs | Pestaña Devoluciones | P33, P35 |
| 23 | Visitor | `IVisitanteCatalogo`, `DesgloseSkuVisitor`, `PesoLogisticoVisitor` | Domain/Catalogo/Visitantes.cs | Catálogo → desglose del kit | P34 |

Patrones de arquitectura: Repository de escritura por agregado (Application/Puertos/Repositorios.cs +
Infrastructure/Persistencia/Repositorios.cs), Unit of Work (`UnidadDeTrabajoEf`, transacción real), CQRS básico
(lectura: `IServicioConsultas` + `IConsultasLectura` AsNoTracking; escritura: servicios de comando), Outbox.
Contratos de casos de uso (DIP): Application/Contratos/Contratos.cs. Valores del caso: Domain/Comun/ParametrosNegocio.cs.
