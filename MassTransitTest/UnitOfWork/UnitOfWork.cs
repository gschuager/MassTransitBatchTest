using System.Threading.Tasks;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {

        public Task Complete()
        {
            throw new System.Exception("error committing transaction");
        }
    }
}