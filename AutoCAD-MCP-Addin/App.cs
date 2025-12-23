using System;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;

namespace AutoCADMCP
{
    public class App : IExtensionApplication
    {
        public static System.Threading.SynchronizationContext MainThreadContext { get; private set; }

        public void Initialize()
        {
            MainThreadContext = System.Threading.SynchronizationContext.Current;
            Application.Idle += Application_Idle;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            Application.Idle -= Application_Idle;
            CreateRibbon();
            
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.Editor.WriteMessage("\n[MCP] AutoCAD MCP Tools Loaded.\n");
            }
        }

        public void Terminate()
        {
            Server.SocketServer.Stop();
        }

        private void CreateRibbon()
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            string tabId = "MCP_TAB";
            RibbonTab tab = ribbon.FindTab(tabId);

            if (tab == null)
            {
                tab = new RibbonTab
                {
                    Id = tabId,
                    Title = "MCP Tools"
                };
                ribbon.Tabs.Add(tab);
            }

            RibbonPanelSource panelSource = new RibbonPanelSource
            {
                Title = "Connection"
            };
            RibbonPanel panel = new RibbonPanel
            {
                Source = panelSource
            };
            tab.Panels.Add(panel);

            RibbonButton btnStart = new RibbonButton
            {
                Text = "Start Server",
                ShowText = true,
                ShowImage = false, // No icons in repo, disable to avoid blank space
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandHandler = new RelayCommand(StartServer)
            };
            
            RibbonButton btnStop = new RibbonButton
            {
                Text = "Stop Server",
                ShowText = true,
                ShowImage = false, // No icons in repo
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandHandler = new RelayCommand(StopServer)
            };

            RibbonRowPanel row = new RibbonRowPanel();
            row.Items.Add(btnStart);
            row.Items.Add(btnStop);
            panel.Source.Items.Add(row);

            tab.IsActive = true;
        }

        [CommandMethod("STARTMCP")]
        public void StartMcpCommand()
        {
            Server.SocketServer.Start();
        }

        private void StartServer(object parameter)
        {
            Server.SocketServer.Start();
        }

        private void StopServer(object parameter)
        {
            Server.SocketServer.Stop();
        }
    }

    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) { _execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
