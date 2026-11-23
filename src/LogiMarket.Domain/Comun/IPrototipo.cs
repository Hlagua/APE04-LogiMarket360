namespace LogiMarket.Domain.Comun;

//patron prototype (creacional)
//interfaz IPrototipo
//objetivo: contrato de clonacion profunda
public interface IPrototipo<out T>
{
    T Clonar();
}
