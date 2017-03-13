using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.Media.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Shapes;
using Windows.Media.FaceAnalysis;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;

namespace Miyop.HDYF
{
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture;
        private bool _isPreviewing;
        private DisplayRequest _displayRequest;
        private FaceDetectionEffect _faceDetectionEffect;
        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private readonly double lineThickness = 2.0;
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        private EmotionServiceClient _emotionClient;
        public MainPage()
        {
            this.InitializeComponent();

            Application.Current.Suspending += Current_Suspending;
            _emotionClient = new EmotionServiceClient("KEY");



        }


        private async void InitializeFaceDetection()
        {
            var definition = new FaceDetectionEffectDefinition();
            definition.SynchronousDetectionEnabled = false;
            definition.DetectionMode = FaceDetectionMode.HighPerformance;
            _faceDetectionEffect = (FaceDetectionEffect)await _mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);

            _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);

            _faceDetectionEffect.Enabled = true;


            _faceDetectionEffect.FaceDetected += _faceDetectionEffect_FaceDetected;

        }



        private async void _faceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.VisualizationCanvas.Children.Clear();
            });


            foreach (Windows.Media.FaceAnalysis.DetectedFace face in args.ResultFrame.DetectedFaces)
            {
                BitmapBounds faceRect = face.FaceBox;


                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Rectangle box = new Rectangle();
                    box.Tag = face.FaceBox;
                    box.Width = (uint)(face.FaceBox.Width);
                    box.Height = (uint)(face.FaceBox.Height);
                    box.Fill = this.fillBrush;
                    box.Stroke = this.lineBrush;
                    box.StrokeThickness = this.lineThickness;

                    box.Margin = new Thickness((uint)(face.FaceBox.X + 70), (uint)(face.FaceBox.Y + 150), 0, 0);

                    this.VisualizationCanvas.Children.Add(box);


                });
            }

        }



        private async void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanupCameraAsync();
                deferral.Complete();
            }

        }

        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {

            await CleanupCameraAsync();
        }

        private async Task StartPreviewAsync()
        {
            try
            {

                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync();
                InitializeFaceDetection();
                PreviewControl.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();
                _isPreviewing = true;

                _displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;


            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                System.Diagnostics.Debug.WriteLine("The app was denied access to the camera");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MediaCapture initialization failed. {0}", ex.Message);
            }
        }
        private async Task CleanupCameraAsync()
        {
            if (_mediaCapture != null)
            {
                if (_isPreviewing)
                {
                    await _mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PreviewControl.Source = null;
                    if (_displayRequest != null)
                    {
                        _displayRequest.RequestRelease();
                    }

                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                });
            }

        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {

            await StartPreviewAsync();
        }

        private async void button_Copy_Click(object sender, RoutedEventArgs e)
        {
            await CleanupCameraAsync();
        }

        private async void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtResult.Text = String.Empty;
                var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
                StorageFile file = await myPictures.SaveFolder.CreateFileAsync("photo.jpg", CreationCollisionOption.ReplaceExisting);

                using (var captureStream = new InMemoryRandomAccessStream())
                {
                    await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);
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

                file = await myPictures.SaveFolder.GetFileAsync("photo.jpg");
                Stream s = await file.OpenStreamForReadAsync();

                Emotion[] emotionResult = await _emotionClient.RecognizeAsync(s);
                
                foreach (var emotion in emotionResult.ToList())
                {
                    txtResult.Text += "Anger: "+emotion.Scores.Anger + Environment.NewLine;
                    txtResult.Text += "Contempt: "+emotion.Scores.Contempt + Environment.NewLine;
                    txtResult.Text += "Disgust: "+emotion.Scores.Disgust + Environment.NewLine;
                    txtResult.Text += "Fear: "+emotion.Scores.Fear + Environment.NewLine;
                    txtResult.Text += "Hapiness: "+emotion.Scores.Happiness + Environment.NewLine;
                    txtResult.Text += "Neutral: "+emotion.Scores.Neutral + Environment.NewLine;
                    txtResult.Text += "Sadness: " + emotion.Scores.Sadness + Environment.NewLine;
                    txtResult.Text += "Suprise: " + emotion.Scores.Surprise + Environment.NewLine;
                    txtResult.Text += "Overall: " + emotion.Scores.ToRankedList().First().Key;
                    
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
