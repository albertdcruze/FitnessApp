using System.Threading.Tasks;

namespace FitTrack.ViewModels;

internal interface INavigationAware
{
    Task OnNavigatedToAsync();
}
