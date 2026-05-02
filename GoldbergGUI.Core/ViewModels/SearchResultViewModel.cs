using GoldbergGUI.Core.Models;
using Microsoft.Extensions.Logging;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.Plugin.Messenger;
using MvvmCross.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoldbergGUI.Core.ViewModels
{
    public class SearchResultViewModel(ILoggerFactory logProvider, IMvxNavigationService navigationService, IMvxMessenger messenger) : MvxNavigationViewModel<IEnumerable<SteamApp>>(logProvider, navigationService), IMvxViewModel<IEnumerable<SteamApp>>
    {
        private readonly ILogger<SearchResultViewModel> _log = logProvider.CreateLogger<SearchResultViewModel>();
        private readonly IMvxMessenger _messenger = messenger;
        private IEnumerable<SteamApp> _apps;

        public override void Prepare(IEnumerable<SteamApp> parameter)
        {
            Apps = parameter;
        }

        public IEnumerable<SteamApp> Apps
        {
            get => _apps;
            set
            {
                SetProperty(ref _apps, value);
            }
        }

        public SteamApp Selected
        {
            get;
            set;
        }

        public IMvxCommand SaveCommand => new MvxAsyncCommand(Save);

        public IMvxCommand CloseCommand => new MvxAsyncCommand(Close);

        public TaskCompletionSource<object> CloseCompletionSource { get; set; }

        public override void ViewDestroy(bool viewFinishing = true)
        {
            if (viewFinishing && CloseCompletionSource != null && !CloseCompletionSource.Task.IsCompleted &&
                !CloseCompletionSource.Task.IsFaulted)
                CloseCompletionSource?.TrySetCanceled();

            base.ViewDestroy(viewFinishing);
        }

        private async Task Save()
        {
            if (Selected != null)
            {
                _log.LogInformation("Successfully got app {AppName}", Selected.Name);
                _messenger.Publish(new AppSelectedMessage(this, Selected));
                await NavigationService.Close(this).ConfigureAwait(false);
            }
        }

        private async Task Close()
        {
            await NavigationService.Close(this).ConfigureAwait(false);
        }
    }
}