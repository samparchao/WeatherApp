// Created by Samuel Teixera Parchao
// Last modified: 13/02/2026

using Microsoft.UI.Xaml;


namespace WeatherApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window m_window;

        /// <summary>
        /// Initializes a new instance of the App class.
        /// </summary>
        /// <remarks>This constructor sets up the application's components and prepares it for execution.
        /// Typically called by the framework when the application starts.</remarks>
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
