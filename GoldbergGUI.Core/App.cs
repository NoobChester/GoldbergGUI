using GoldbergGUI.Core.Utils;
using GoldbergGUI.Core.ViewModels;
using MvvmCross;
using MvvmCross.IoC;
using MvvmCross.Plugin.Messenger;
using MvvmCross.ViewModels;

namespace GoldbergGUI.Core
{
    public class App : MvxApplication
    {
        public override void Initialize()
        {
            Mvx.IoCProvider.RegisterSingleton<IMvxMessenger>(new MvxMessengerHub());
            CreatableTypes()
                .EndingWith("Service")
                .AsInterfaces()
                .RegisterAsLazySingleton();
            //RegisterAppStart<MainViewModel>();
            RegisterCustomAppStart<CustomMvxAppStart<MainViewModel>>();
        }
    }
}