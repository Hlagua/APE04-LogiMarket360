# LogiMarket 360 — APE 04

Aplicación de escritorio (Windows Forms, .NET 8) para ventas y logística de accesorios de computación, con
Clean Architecture, SQLite + EF Core 8, los 23 patrones GoF integrados en flujos reales y pruebas automatizadas.

## Requisitos

- Windows 10/11.
- SDK de .NET 8 o superior (probado con SDK 10.0.401 compilando `net8.0`/`net8.0-windows`) y runtime **Microsoft.WindowsDesktop.App 8.x**.
- Visual Studio 2022 (17.8+) o `dotnet` CLI.
- Acceso a NuGet para restaurar paquetes (EF Core 8.0.11, Microsoft.Extensions.DependencyInjection 8.0.1, xUnit).

## Estructura

```
LogiMarket360.sln
├─ src/LogiMarket.Domain          → sin referencias (entidades, invariantes, State, Composite, Strategy…)
├─ src/LogiMarket.Application     → Domain (casos de uso, puertos, DTO, validación, orquestación)
├─ src/LogiMarket.Infrastructure  → Application + Domain (EF Core/SQLite, repositorios, adaptadores, Outbox)
├─ src/LogiMarket.WinForms        → Application + Domain (tipos) + Infrastructure (solo para registrar DI en Program.cs)
└─ tests/LogiMarket.Tests         → Domain + Application + Infrastructure (xUnit)
```

## Instalación, compilación y ejecución

```powershell
dotnet restore LogiMarket360.sln
dotnet tool restore                      # instala dotnet-ef 8.0.11 (herramienta local)
dotnet build LogiMarket360.sln -c Release
dotnet run --project src/LogiMarket.WinForms
```

Al iniciar, la aplicación **aplica las migraciones EF Core y ejecuta la semilla idempotente**. Si el archivo
de base no existe, lo crea desde cero (no hace falta editar tablas a mano). Ubicación por defecto:
`src/LogiMarket.WinForms/bin/<Config>/net8.0-windows/datos/logimarket.db` (se puede cambiar con la variable de
entorno `LOGIMARKET_DB`). Comprobantes TXT y avisos simulados: carpeta `salidas/`; bitácora técnica: `logs/`.

Configuración opcional: copie `appsettings.ejemplo.json` (junto al ejecutable) como `appsettings.json` y cambie
`RutaBaseDatos`, `CarpetaSalidas` o `CarpetaLogs`. Las rutas relativas parten de la carpeta del ejecutable. Prioridad:
`LOGIMARKET_DB` → `appsettings.json` → valores por defecto. El archivo no lleva credenciales.

Aplicar las migraciones manualmente (opcional):

```powershell
dotnet ef database update -p src/LogiMarket.Infrastructure -s src/LogiMarket.Infrastructure
```

## Cuentas de demostración (ficticias)

| Usuario | Contraseña | Rol | Permisos |
|---|---|---|---|
| admin | Admin123! | ADMIN | todas las operaciones, demostración y restablecimiento |
| ventas | Ventas123! | VENTAS | clientes, borradores, validación, cobro, cancelación, devoluciones |
| bodega | Bodega123! | BODEGA | despacho y entrega |
| consulta | Consulta123! | CONSULTA | solo lectura |

En la base solo se guarda el **hash PBKDF2-SHA256** con sal. Son credenciales de demostración, no productivas.
Para comprobar a mano un hash de la tabla `Usuario`: `./scripts/verificar-hash.ps1 -Clave 'Admin123!' -Hash '<valor de HashContrasena>'`.

Clientes semilla: **Ana** (VIP), **Luis** (NORMAL), **Carmen** (MAYORISTA).

## Pruebas automatizadas

```powershell
dotnet test LogiMarket360.sln
```

Cada prueba de integración crea su propia base SQLite temporal (migrada y sembrada), por lo que las cifras
oficiales (59.00 / 102.00 / 90.00, rechazo de KIT_TELETRABAJO×6, reembolso 54.00) se verifican sobre datos
controlados. El nombre de cada prueba indica el caso de la matriz (P01–P38). Las pruebas corren en serie porque
`MetricasSesion` es un Singleton real.

Cada ejecución genera la tabla de evidencia **entrada / esperado / obtenido** en
`tests/LogiMarket.Tests/bin/Debug/net8.0/evidencias/matriz-esperado-obtenido.md`.

## Restablecer los datos de prueba de forma segura

- Desde la app: iniciar sesión como **admin** → pestaña **12·Demostración** → *Restablecer base de pruebas*
  (pide confirmación; borra la base, aplica migraciones y vuelve a sembrar).
- Manualmente: cerrar la aplicación y eliminar `datos/logimarket.db`; se recrea en el siguiente inicio.

## Escenarios de demostración

Pestaña **12·Demostración** (solo ADMIN): cambiar el proveedor simulado (LEGADO vía Adapter / MODERNO), el canal
de aviso (EMAIL / SMS), forzar rechazo o error del cobro y forzar fallo del notificador (el evento queda pendiente
en el Outbox y se recupera con *Procesar Outbox pendiente*). Estas opciones solo afectan a colaboradores simulados y
cada cambio queda en la bitácora (categoría DEMO).

## Empaquetar la entrega

```powershell
./scripts/empaquetar.ps1 -Nombre "APELLIDOS_NOMBRES"
```

Genera `APE04_LogiMarket360_<Nombre>.zip` sin `bin/`, `obj/`, `.vs/`, bases `.db`, `logs/`, `salidas/`, `entrega/`
(informe y evidencias), el enunciado (`GUIA_APE_04.docx`), `CLAUDE.md` ni un `appsettings.json` local.

## Documentación técnica

- `docs/MATRIZ_PATRONES.md`: patrón → clases → archivo → prueba.
- `docs/BITACORA_DECISIONES.md`: decisiones de diseño y cambios fechados.
