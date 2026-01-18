using System.Collections.Generic;
using System.Threading.Tasks;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IHistoryService
{
    Task InitializeAsync();
    Task<IEnumerable<HistoryEntity>> SearchAsync(string query, int skip, int take);
    Task DeleteItemsAsync(IEnumerable<HistoryEntity> items);
}
