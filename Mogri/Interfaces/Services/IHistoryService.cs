using System.Collections.Generic;
using System.Threading.Tasks;
using Mogri.Models;

namespace Mogri.Interfaces.Services;

public interface IHistoryService
{
    Task<bool> InitializeAsync();
    Task<IEnumerable<HistoryEntity>> SearchAsync(string query, int skip, int take, bool isHidden = false);
    Task SetItemsHiddenAsync(IEnumerable<HistoryEntity> items, bool isHidden);
    Task DeleteItemsAsync(IEnumerable<HistoryEntity> items);
}
