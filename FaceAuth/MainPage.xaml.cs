using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.Graphics.Display;
using System.Diagnostics;
using Windows.UI.Core;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
using Windows.Storage.FileProperties;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

// Face API Example https://docs.microsoft.com/en-us/azure/cognitive-services/face/face-api-how-to-topics/howtoidentifyfacesinimage

namespace FaceAuth
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture mediaCapture;
        bool isPreviewing;
        DisplayRequest displayRequest = new DisplayRequest();

        //Register for a free Face API key and URL at https://azure.microsoft.com/en-us/try/cognitive-services/
        static string faceApiKey = "<Insert API key>";
        static string faceApiUrl = "<Insert API URL>";

        FaceServiceClient faceServiceClient = new FaceServiceClient(faceApiKey, faceApiUrl);

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Authenticate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBoxPersonGroupId.Text))
            {
                LogMessage($"Error: Person Group Id is empty.");
            }
            else
            {

                StorageFolder projectFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("FaceAuth", CreationCollisionOption.OpenIfExists);
                StorageFile file = await projectFolder.CreateFileAsync($"authenticate.jpg", CreationCollisionOption.ReplaceExisting);

                playSound.Play();

                using (var captureStream = new InMemoryRandomAccessStream())
                {
                    await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

                    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var decoder = await BitmapDecoder.CreateAsync(captureStream);
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                        var properties = new BitmapPropertySet {
                            { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
                        };
                        await encoder.BitmapProperties.SetPropertiesAsync(properties);

                        await encoder.FlushAsync();
                    }
                }

                string testImageFile = ApplicationData.Current.LocalFolder.Path + "\\FaceAuth\\authenticate.jpg";

                using (Stream s = File.OpenRead(testImageFile))
                {
                    var faces = await faceServiceClient.DetectAsync(s);
                    if (faces.Length > 0)
                    {
                        var faceIds = faces.Select(face => face.FaceId).ToArray();

                        try
                        {
                            var results = await faceServiceClient.IdentifyAsync(txtBoxPersonGroupId.Text, faceIds);

                            foreach (var identifyResult in results)
                            {
                                Debug.WriteLine("Result of face: {0}", identifyResult.FaceId);
                                if (identifyResult.Candidates.Length == 0)
                                {
                                    LogMessage("No one identified");
                                }
                                else
                                {
                                    // Get top 1 among all candidates returned
                                    var candidateId = identifyResult.Candidates[0].PersonId;
                                    var person = await faceServiceClient.GetPersonAsync(txtBoxPersonGroupId.Text, candidateId);
                                    LogMessage($"Identified as: {person.Name}");
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            LogMessage($"{error.Message}");
                            return;
                        }
                    }
                    else
                    {
                        LogMessage("No face identified. Please try again.");
                    }
                }
            }
        }

        private async Task StartPreviewAsync()
        {
            try
            {

                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                LogMessage("The app was denied access to the camera.");
                return;
            }

            try
            {
                PreviewControl.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (System.IO.FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }

        }

        private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                LogMessage("The camera preview can't be displayed because another app has exclusive access.");
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await StartPreviewAsync();
                });
            }
        }

        private async Task CleanupCameraAsync()
        {
            if (mediaCapture != null)
            {
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PreviewControl.Source = null;
                    if (displayRequest != null)
                    {
                        displayRequest.RequestRelease();
                    }

                    mediaCapture.Dispose();
                    mediaCapture = null;
                });
            }

        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await StartPreviewAsync();
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await CleanupCameraAsync();
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder projectFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("FaceAuth", CreationCollisionOption.OpenIfExists);
            StorageFile file = await projectFolder.CreateFileAsync($"{txtBoxPerson.Text}.jpg", CreationCollisionOption.GenerateUniqueName);

            playSound.Play();

            using (var captureStream = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var decoder = await BitmapDecoder.CreateAsync(captureStream);
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                    var properties = new BitmapPropertySet {
                        { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
                    };
                    await encoder.BitmapProperties.SetPropertiesAsync(properties);

                    await encoder.FlushAsync();

                    LogMessage($"Photo saved to {projectFolder.Path}");

                }
            }
        }

        private async void CreatePersonGroup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBoxPersonGroupId.Text))
            {
                LogMessage($"Error: Person Group Id is empty.");
            }
            else if (string.IsNullOrWhiteSpace(txtBoxPersonGroup.Text))
            {
                LogMessage($"Error: Person Group Name is empty.");
            }
            else
            {
                try
                {
                    await faceServiceClient.CreatePersonGroupAsync(txtBoxPersonGroupId.Text, txtBoxPersonGroup.Text);
                }
                catch (Exception error)
                {
                    LogMessage($"{error.Message}");
                    return;
                }

                LogMessage($"Person group created");
            }
        }

        private async void CreatePerson_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBoxPersonGroupId.Text))
            {
                LogMessage($"Error: Person Group Id is empty.");
            }
            else if (string.IsNullOrWhiteSpace(txtBoxPerson.Text))
            {
                LogMessage($"Error: Person name is empty.");
            }
            else
            {
                try
                {
                    //Create the person
                    CreatePersonResult person = await faceServiceClient.CreatePersonAsync(txtBoxPersonGroupId.Text, txtBoxPerson.Text);

                    //Register the person
                    string projectFolder = ApplicationData.Current.LocalFolder.Path + "\\FaceAuth";
                    foreach (string imagePath in Directory.GetFiles(projectFolder, $"{txtBoxPerson.Text}*.jpg"))
                    {
                        using (Stream s = File.OpenRead(imagePath))
                        {
                            // Detect faces in the image and add
                            await faceServiceClient.AddPersonFaceAsync(
                                txtBoxPersonGroupId.Text, person.PersonId, s);
                        }

                        LogMessage($"Person registered");
                    }
                }
                catch (Exception error)
                {
                    LogMessage($"{error.Message}");
                    return;
                }
            }
        }

        private async void Train_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtBoxPersonGroupId.Text))
                {
                    LogMessage($"Error: Person Group Id is empty.");
                }
                else
                {
                    await faceServiceClient.TrainPersonGroupAsync(txtBoxPersonGroupId.Text);
                    LogMessage($"Model trained.");
                }

            }
            catch (Exception error)
            {
                LogMessage($"{error.Message}");
                return;
            }
        }

        private void txtBoxOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(txtBoxOutput, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        private void LogMessage(string message)
        {
            txtBoxOutput.Text += $"{message}\n";
            Debug.WriteLine($"{message}");
        }
    }
}
