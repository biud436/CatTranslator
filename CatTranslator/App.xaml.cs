using System.Windows;
using System.Windows.Threading;
using CatTranslator.Core;

namespace CatTranslator;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\CatTranslator.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("고양이 번역기가 이미 실행 중입니다.", "고양이 번역기");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        var settings = SettingsStore.Load();
        var history = HistoryStore.Load(settings.Current.SaveHistory);

        var cat = new CatWindow(settings, history);
        cat.Closed += (_, _) => Shutdown();
        cat.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstance?.ReleaseMutex();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "고양이 번역기 - 예상하지 못한 오류",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
