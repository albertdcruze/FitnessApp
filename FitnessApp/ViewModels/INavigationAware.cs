using System.Threading.Tasks;

namespace FitnessApp.ViewModels;

internal interface INavigationAware
{
    Task OnNavigatedToAsync();
}
