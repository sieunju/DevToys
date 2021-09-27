﻿#nullable enable

using DevToys.Api.Core;
using DevToys.Api.Core.Injection;
using DevToys.Api.Core.Navigation;
using DevToys.Api.Core.Settings;
using DevToys.Api.Core.Theme;
using DevToys.Core;
using DevToys.Core.Settings;
using DevToys.Core.Threading;
using DevToys.Views;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace DevToys
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application, IDisposable
    {
        private readonly Task<MefComposer> _mefComposer;
        private readonly Lazy<Task<IThemeListener>> _themeListener;

        private bool _isDisposed;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Set the language of the app for startup. By default, it's the same than Windows, or english.
            // The language defined by the user will be applied later, once MEF is loaded, but before the UI shows up.
            LanguageManager.Instance.SetCurrentCulture(CultureInfo.InstalledUICulture);

            // Initialize MEF
            _mefComposer
                = Task.Run(() =>
                {
                    return new MefComposer(
                        typeof(MefComposer).Assembly);
                });

            UnhandledException += OnUnhandledException;

            // Importing it in a Lazy because we can't import it before a Window is created.
            _themeListener = new Lazy<Task<IThemeListener>>(async () => (await _mefComposer).ExportProvider.GetExport<IThemeListener>());

            InitializeComponent();
            Suspending += OnSuspending;
        }

        ~App()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                _mefComposer?.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = await EnsureWindowIsInitializedAsync();

            var mefComposer = await _mefComposer;
            if (e.PrelaunchActivated == false)
            {
                // On Windows 10 version 1607 or later, this code signals that this app wants to participate in prelaunch
                CoreApplication.EnablePrelaunch(true);

                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(
                        typeof(MainPage),
                        new NavigationParameter(
                            mefComposer.ExportProvider.GetExport<IMefProvider>(),
                            query: e.Arguments),
                        new SuppressNavigationTransitionInfo());
                }

                // Ensure the current window is active
                Window.Current.Activate();
            }
            else
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(
                        typeof(MainPage),
                        new NavigationParameter(
                            mefComposer.ExportProvider.GetExport<IMefProvider>(),
                            query: e.Arguments),
                        new SuppressNavigationTransitionInfo());
                }
            }

            // Setup the title bar.
            mefComposer.ExportProvider.GetExport<ITitleBar>().SetupTitleBarAsync().Forget();
        }

        /// <summary>
        /// Invoked when the application is activated by some means other than normal launching.
        /// </summary>
        protected override async void OnActivated(IActivatedEventArgs args)
        {
            Frame rootFrame = await EnsureWindowIsInitializedAsync();

            var mefComposer = await _mefComposer;
            if (args.Kind == ActivationKind.Protocol)
            {
                var eventArgs = (ProtocolActivatedEventArgs)args;
                rootFrame.Navigate(
                    typeof(MainPage),
                    new NavigationParameter(
                        mefComposer.ExportProvider.GetExport<IMefProvider>(),
                        query: eventArgs.Uri.Query));

                // Ensure the current window is active
                Window.Current.Activate();

                // Setup the title bar.
                mefComposer.ExportProvider.GetExport<ITitleBar>().SetupTitleBarAsync().Forget();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogFault("Unhandled problem", e.Exception);
        }

        private async Task<Frame> EnsureWindowIsInitializedAsync()
        {
            ApplicationView applicationView = ApplicationView.GetForCurrentView();
            applicationView.SetPreferredMinSize(new Windows.Foundation.Size(300, 200));

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (Window.Current.Content is not Frame rootFrame)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();
                rootFrame.CacheSize = 10;
                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            // Set the user-defined language.
            string languageIdentifier = (await _mefComposer).ExportProvider.GetExport<ISettingsProvider>().GetSetting(PredefinedSettings.Language);
            LanguageDefinition languageDefinition = LanguageManager.Instance.AvailableLanguages.First(l => string.Equals(l.Identifier, languageIdentifier));
            LanguageManager.Instance.SetCurrentCulture(languageDefinition.Culture);

            // Apply the app color theme.
            (await _themeListener.Value).ApplyDesiredColorTheme();

            return rootFrame;
        }
    }
}