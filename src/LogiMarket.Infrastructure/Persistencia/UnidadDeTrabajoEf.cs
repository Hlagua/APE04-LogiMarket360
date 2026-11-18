using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>
/// Unidad de Trabajo real: DbContext + transacción de SQLite (BEGIN IMMEDIATE, que serializa a los
/// escritores). Traduce los conflictos de EF a <see cref="ExcepcionConcurrencia"/> para Application.
/// </summary>
public sealed class UnidadDeTrabajoEf : IUnidadDeTrabajo
{
    private readonly LogiMarketDbContext _db;

    public UnidadDeTrabajoEf(LogiMarketDbContext db) => _db = db;

    public async Task GuardarCambiosAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ExcepcionConcurrencia("Otro usuario modificó el registro (concurrencia optimista).", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite)
        {
            throw new ExcepcionConcurrencia($"Restricción de integridad: {sqlite.Message}", ex);
        }
    }

    public async Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is not null)
            return await operacion(ct); // transacción ya abierta: se reutiliza

        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var resultado = await operacion(ct);
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public void DescartarCambios() => _db.ChangeTracker.Clear();
}
