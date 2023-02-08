using System.Threading.Tasks;

namespace MassTransitTest.UnitOfWork
{
    public interface IUnitOfWork
    {
        Task Complete();
    }
}