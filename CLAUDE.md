# LogiMarket 360 (APE 04) — contexto para cada sesión

Proyecto universitario: app WinForms .NET 8 + SQLite/EF Core 8, Clean Architecture con 5 proyectos y los 23
patrones GoF integrados. Enunciado completo: `GUIA_APE_04.docx`. Responder siempre en español (Ecuador).

## Antes de cambiar algo

1. Leer `docs/BITACORA_DECISIONES.md` (decisiones vigentes y por qué) y `docs/MATRIZ_PATRONES.md` (dónde vive cada patrón).
2. Al terminar un cambio: agregar una entrada fechada en la bitácora y actualizar la matriz si cambia un patrón.
3. El informe PDF NO se hace hasta que el usuario apruebe el sistema completo.

## Reglas que no se pueden romper (guía APE 04)

- Dependencias: Domain → nada; Application → Domain; Infrastructure → Application/Domain; WinForms → Application
  (+ Infrastructure solo en `Program.cs` para DI); Tests → Domain/Application/Infrastructure.
- Nada de EF/SQL en Domain o Application; nada de cálculos de tarifa/descuento en formularios; formularios no se
  llaman entre sí (usar `FormularioPrincipalMediator`).
- Autorización en Application (`IServicioAutorizacion`, `PagoAutorizadoProxy`), nunca solo en botones.
- Cifras oficiales: Ana 59.00 (P01 7, P02 4, P03 19); Luis 102.00; Carmen 90.00; KIT_TELETRABAJO×6 rechazado;
  reembolso de Ana 54.00. `decimal` + `MidpointRounding.AwayFromZero`.
- No cambiar reglas numéricas para que coincidan con capturas ni ocultar pruebas fallidas.

## Comandos

```powershell
dotnet build LogiMarket360.sln
dotnet test LogiMarket360.sln
dotnet run --project src/LogiMarket.WinForms
dotnet ef migrations add <Nombre> -p src/LogiMarket.Infrastructure -s src/LogiMarket.Infrastructure -o Persistencia/Migraciones
```

`dotnet ef` es una herramienta local (`dotnet tool restore`). Cuentas demo: admin/Admin123!, ventas/Ventas123!,
bodega/Bodega123!, consulta/Consulta123!.

## Mapa rápido

- Dominio: `src/LogiMarket.Domain` (Pedido + State/Memento/Builder/Prototype, Catálogo Composite/Iterator/Visitor, Strategy).
- Casos de uso: `src/LogiMarket.Application` (Facade de confirmación, Chain, Command, Template Method, Interpreter,
  Bridge, Flyweight, Observer/Outbox, Proxy, Decorator, Abstract Factory, Factory Method, Singleton de métricas).
- Composición DI: `src/LogiMarket.Infrastructure/RegistroDependencias.cs` (la usan la app y las pruebas).
- UI: `src/LogiMarket.WinForms/Vistas/*` (una vista por pestaña), `Infraestructura/Mediador.cs`, `Ejecutor.cs`.
- Pruebas: `tests/LogiMarket.Tests` (DisplayName con el caso P01–P38). Soporte: `Soporte/EntornoPrueba.cs`.
