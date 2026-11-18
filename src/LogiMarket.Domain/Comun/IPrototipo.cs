namespace LogiMarket.Domain.Comun;

/// <summary>Patrón Prototype: contrato de clonación profunda.</summary>
public interface IPrototipo<out T>
{
    T Clonar();
}
