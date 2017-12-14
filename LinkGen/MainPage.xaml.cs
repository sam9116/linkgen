using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.System;
using Windows.ApplicationModel;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.System.Display;
using Windows.UI.Core;
using System.Diagnostics;
using Windows.Storage;
using Windows.Media.Devices;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Collections.Generic;
using SDKTemplate;
using System.IO;




// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LinkGen
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 


    public sealed partial class MainPage : Page
    {
        OcrEngine ocrEngine;
        private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private DisplayOrientations _displayOrientation = DisplayOrientations.Landscape;
        urlextension r = new urlextension();
        bool Save = false;
        // Rotation metadata to apply to the preview stream (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        // MediaCapture and its state variables
        private MediaCapture _mediaCapture;
        private bool _isInitialized = false;
        private bool _isPreviewing = false;

        // Information about the camera device
        private bool _mirroringPreview = false;
        private bool _externalCamera = false;


        // Folder in which the captures will be stored (initialized in InitializeCameraAsync)

        private StorageFolder _captureFolder = null;

        IEnumerable<StreamResolution> allPreviewProperties;

        #region Constructor, lifecycle and navigation
        public MainPage()
        {
            this.InitializeComponent();
            ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("en"));
            // Cache the UI to have the checkboxes retain their state, as the enabled/disabled state of the
            // GetPreviewFrameButton is reset in code when suspending/navigating (see Start/StopPreviewAsync)

            // Useful to know when to initialize/clean up the camera


        }


        private bool IsVisibileToUser(FrameworkElement element, FrameworkElement container)
        {
            if (element == null || container == null)
                return false;


            Rect elementBounds = element.TransformToVisual(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            Rect containerBounds = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);

            return (elementBounds.Bottom <= containerBounds.Top && elementBounds.Top >= containerBounds.Bottom);
        }

        private void Button_clicked(object sender, RoutedEventArgs e)
        {
            //greetingOutput.Text = "Hello, " + nameInput.Text + "!";

            Button b = (Button)sender;
            StackPanel l = (StackPanel)b.Parent;
            TextBox y = (TextBox)l.Children[1];
            Enter_pressed(y);

        }

        private void Enter_press(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Enter_pressed((TextBox)sender);

            }
        }

        private async void Enter_pressed(TextBox nameInput)
        {
            if (!nameInput.Text.Contains("http://") && !nameInput.Text.Contains("https://") && !nameInput.Text.Contains("@"))
                nameInput.Text = "http://" + nameInput.Text;
            if (nameInput.Text.Contains("@"))
            {
                nameInput.Text = "mailto:" + nameInput.Text;
            }
            Uri link = new Uri(nameInput.Text, UriKind.Absolute);

            await Launcher.LaunchUriAsync(link);

        }

        private async void Load_image(SoftwareBitmap bitmap)
        {

            listofurl.Items.Clear();

            OcrResult result = await ocrEngine.RecognizeAsync(bitmap);

            string[] ssize = result.Text.Split(' ');
            foreach (string s in ssize)
            {
                foreach (string c in r.knownextensions)
                {
                    if (s.Contains(c))
                    {
                        //filter = s;

                        ListViewItem item = new ListViewItem();
                        Button gobutton = new Button();
                        TextBox urlbox = new TextBox();
                        StackPanel holder = new StackPanel();
                        holder.Orientation = Orientation.Horizontal;

                        gobutton.Content = "Go";
                        gobutton.Click += Button_clicked;
                        gobutton.HorizontalAlignment = HorizontalAlignment.Right;


                        urlbox.Margin = (new Thickness(3));
                        urlbox.MaxWidth = 300;

                        urlbox.Text = s;
                        urlbox.KeyDown += Enter_press;

                        holder.Children.Add(gobutton);
                        holder.Children.Add(urlbox);

                        item.Content = holder;
                        listofurl.Items.Add(item);
                        //inputButton.Content = "Go";
                    }
                }
            }
            // Display recognized text.
            // nameInput.Text = filter;

        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();

                await CleanupCameraAsync();

                _systemMediaControls.PropertyChanged -= SystemMediaControls_PropertyChanged;
                _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;

                deferral.Complete();
            }
        }

        private async void Application_Resuming(object sender, object o)
        {

            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                // Populate orientation variables with the current state and register for future changes
                _displayOrientation = _displayInformation.CurrentOrientation;
                _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
                _systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;

                await InitializeCameraAsync();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Populate orientation variables with the current state and register for future changes
            _displayOrientation = _displayInformation.CurrentOrientation;
            _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;



            //Mobile customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                await statusBar.HideAsync();
            }
            await InitializeCameraAsync();
            
            if (_isPreviewing)
            {
                await GetPreviewFrameAsSoftwareBitmapAsync();
            }


        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Handling of this event is included for completenes, as it will only fire when navigating between pages and this sample only includes one page

            await CleanupCameraAsync();

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
        }

        #endregion Constructor, lifecycle and navigation


        #region Event handlers

        /// <summary>
        /// In the event of the app being minimized this method handles media property change events. If the app receives a mute
        /// notification, it is no longer in the foregroud.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Only handle this event if this page is currently being displayed
                if (args.Property == SystemMediaTransportControlsProperty.SoundLevel && Frame.CurrentSourcePageType == typeof(MainPage))
                {
                    // Check to see if the app is being muted. If so, it is being minimized.
                    // Otherwise if it is not initialized, it is being brought into focus.
                    if (sender.SoundLevel == SoundLevel.Muted)
                    {
                        await CleanupCameraAsync();
                    }
                    else if (!_isInitialized)
                    {
                        await InitializeCameraAsync();
                    }
                    else if (_isInitialized)
                    {
                        Debug.WriteLine("Camera is already initialized");
                    }
                }
            });
        }

        /// <summary>
        /// This event will fire when the page is rotated
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data.</param>
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            _displayOrientation = sender.CurrentOrientation;

            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        private async void GetPreviewFrameButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If preview is not running, no preview frames can be acquired
            if (!_isPreviewing) return;
            //await Task.Delay(1000);
            await GetPreviewFrameAsSoftwareBitmapAsync();



        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await CleanupCameraAsync();

            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => LinkGen.IsEnabled = _isPreviewing);
        }

        #endregion Event handlers


        #region MediaCapture methods

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, and starts preview
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            if (_mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }


                var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);

                // Fall back to the local app storage if the Pictures Library is not available

                _captureFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;

                // Create MediaCapture and its settings
                _mediaCapture = new MediaCapture();

                // Register for a notification when something goes wrong
                _mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };


                // Initialize MediaCapture
                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    await CleanupCameraAsync();
                }

                // If initialization succeeded, start the preview
                if (_isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device
                        _externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device
                        _externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel
                        _mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }


                    var flashControl = _mediaCapture.VideoDeviceController.FlashControl;
                    if (flashControl.Supported)
                    {
                        var assustantlight = _mediaCapture.VideoDeviceController.FlashControl.AssistantLightSupported;
                        if (assustantlight)
                        {
                            FlashButton.IsEnabled = true;
                        }
                    }
                    allPreviewProperties = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(x => new StreamResolution(x));

                    await StartPreviewAsync();
                }
            }
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on and unlocks the UI
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            Debug.WriteLine("StartPreviewAsync");

            // Prevent the device from sleeping while the preview is running
            _displayRequest.RequestActive();

            // Register to listen for media property changes
            _systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = _mediaCapture;

            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            // Start the preview
            await _mediaCapture.StartPreviewAsync();
            _isPreviewing = true;

            // Initialize the preview to the current orientation
            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }

            // Enable / disable the button depending on the preview state
            //LinkGen.IsEnabled = _isPreviewing;
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (_externalCamera) return;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

            // The rotation direction needs to be inverted if the preview is being mirrored
            if (_mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            // Query all properties of the device 
            
            //no 16:9 frames, they're a waste of space

            allPreviewProperties = allPreviewProperties.OrderByDescending(x => x.Height * x.Width).OrderByDescending(x=>x.FrameRate);


            List<StreamResolution> r = allPreviewProperties.ToList();
            r.RemoveAll(x => x.AspectRatio>1.5);
            // Order them by resolution then frame rate
            

            await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, r.First().EncodingProperties, null);
            await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, r.First().EncodingProperties);
            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);


            await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
            //await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.Photo, props_photo, null);
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes, and locks the UI
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PreviewControl.Source = null;

                // Allow the device to sleep now that the preview is stopped
                _displayRequest.RequestRelease();

                //LinkGen.IsEnabled = _isPreviewing;
            });
        }

        /// <summary>
        /// Gets the current preview frame as a SoftwareBitmap, displays its properties in a TextBlock, and can optionally display the image
        /// in the UI and/or save it to disk as a jpg
        /// </summary>
        /// <returns></returns>
        private async Task GetPreviewFrameAsSoftwareBitmapAsync()
        {
            // Get information about the preview
            var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

           
            // Create the video frame to request a SoftwareBitmap preview frame
            var videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)previewProperties.Width, (int)previewProperties.Height);

            // Disable tapped control on preview element so User won't overload the phototaking operation
            PreviewControl.Tapped -= GetPreviewFrameButton_Tapped;

            // Capture the preview frame
            var focusControl = _mediaCapture.VideoDeviceController.FocusControl;

            if (focusControl.Supported)
            {
                _mediaCapture.VideoDeviceController.FocusControl.Configure(new FocusSettings { Mode = FocusMode.Auto });
                await _mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
            }


            VideoFrame r = await _mediaCapture.GetPreviewFrameAsync(videoFrame);





            // Get the SoftwareBitmap representation of the file
            SoftwareBitmap previewFrame = r.SoftwareBitmap;

            BitmapImage bmpImage = new BitmapImage();
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                if(_displayOrientation == DisplayOrientations.Portrait)
                {
                    encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
                }
                else if (_displayOrientation == DisplayOrientations.LandscapeFlipped)
                {
                    encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise180Degrees;
                }
                else if (_displayOrientation == DisplayOrientations.PortraitFlipped)
                {
                    encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise270Degrees;
                }

                encoder.SetSoftwareBitmap(previewFrame);
                await encoder.FlushAsync();

                BitmapDecoder rdecoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap rotatedframe = await rdecoder.GetSoftwareBitmapAsync();

                if (Save)
                {
                    var file = await _captureFolder.CreateFileAsync("PreviewFrame.jpg", CreationCollisionOption.GenerateUniqueName);
                    await SaveSoftwareBitmapAsync(rotatedframe, file);
                    Debug.WriteLine("Saving preview frame to " + file.Path);
                }


                Load_image(rotatedframe);

                PreviewControl.Tapped += GetPreviewFrameButton_Tapped;
                Debug.WriteLine("Prewview Tap restored");

            }
            

            
        }

        /// <summary>
        /// Gets the current preview frame as a Direct3DSurface and displays its properties in a TextBlock
        /// </summary>
        /// <returns></returns>

        /// <summary>
        /// Cleans up the camera resources (after stopping the preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            if (_isInitialized)
            {
                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            if (_mediaCapture != null)
            {
                _mediaCapture.Failed -= MediaCapture_Failed;
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }
        }

        #endregion MediaCapture methods


        #region Helper functions

        /// <summary>
        /// Queries the available video capture devices to try and find one mounted on the desired panel
        /// </summary>
        /// <param name="desiredPanel">The panel on the device that the desired camera is mounted on</param>
        /// <returns>A DeviceInformation instance with a reference to the camera mounted on the desired panel if available,
        ///          any other camera if not, or null if no camera is available.</returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// <summary>
        /// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the app on the screen</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Saves a SoftwareBitmap to the Pictures library with the specified name
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private static async Task SaveSoftwareBitmapAsync(SoftwareBitmap bitmap)
        {
            var file = await Package.Current.InstalledLocation.CreateFileAsync("Photo.jpg", CreationCollisionOption.GenerateUniqueName);

            using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);

                // Grab the data from the SoftwareBitmap
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }
        }


        /// <summary>
        /// Saves a SoftwareBitmap to the specified StorageFile
        /// </summary>
        /// <param name="bitmap">SoftwareBitmap to save</param>
        /// <param name="file">Target StorageFile to save to</param>
        /// <returns></returns>

        private static async Task SaveSoftwareBitmapAsync(SoftwareBitmap bitmap, StorageFile file)
        {
            using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);

                // Grab the data from the SoftwareBitmap
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }
        }

        private async void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            await CleanupCameraAsync();
        }



        #endregion Helper functions 

        private void Flash_Click(object sender, RoutedEventArgs e)
        {
            var flashControl = _mediaCapture.VideoDeviceController.FlashControl;
            var assustantlight = _mediaCapture.VideoDeviceController.FlashControl.AssistantLightSupported;
            flashControl.AssistantLightEnabled = !flashControl.AssistantLightEnabled;
            FlashButton.Content = flashControl.AssistantLightEnabled ? "Flash On" : "Flash Off";

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Save = !Save;

            SaveButton.Content = Save ? "Save On" : "Save Off";

        }
    }
}
