using System.Windows;
using Microsoft.Extensions.Hosting;

namespace Chronos;

public partial class App : Application
{
    private IHost _host = null!;
    // Le cycle de vie complet (build host, StartAsync, résolution MainWindow, dispose) est câblé en Task 3.
}
