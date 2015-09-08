using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

//追加
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Windows.Data.Json;
using Windows.UI.Xaml.Shapes;


// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace FaceAPI_UWP
{

    public sealed partial class MainPage : Page
    {
        private MediaCapture capture;
        private StorageFile photoFile;
        private const string ServiceHost = "https://api.projectoxford.ai/face/v0";
        private Boolean isPreviewing;

        /// <summary>
        /// 
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            isPreviewing = false;

            WebcamSetting();
        }

        /// <summary>
        /// 
        /// </summary>
        private async void WebcamSetting()
        {
            try
            {
                if (capture != null)
                {
                    if (isPreviewing)
                    {
                        //キャプチャーオブジェクトが有効な場合は一旦プレビューを停止
                        await capture.StopPreviewAsync();
                        isPreviewing = false;
                    }

                    //キャプチャーオブジェクトのクリア
                    capture.Dispose();
                    capture = null;
                }

                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.VideoDeviceId = "";
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Video;
                captureInitSettings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;

                //Webカメラの接続を確認
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                if (devices.Count() == 0)
                {
                    textBox.Text = "カメラが接続されていません。";
                    return;
                }
                else if (devices.Count() == 1)
                {
                    captureInitSettings.VideoDeviceId = devices[0].Id;
                }
                else
                {
                    captureInitSettings.VideoDeviceId = devices[1].Id;
                }

                capture = new MediaCapture();
                await capture.InitializeAsync(captureInitSettings);

                //Webカメラの利用できる解像度を確認する場合はコメントアウト解除
                //var resolusions = GetPreviewResolusions(capture);

                
                //Webカメラの設定
                //解像度が640X480だとプレビュー表示が乱れる。
                VideoEncodingProperties vp = new VideoEncodingProperties();
                vp.Height = 240;        //高さ
                vp.Width = 320;         //幅
                vp.Subtype = "YUY2";    //形式

                await capture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, vp);

                cap.Source = capture;
                await capture.StartPreviewAsync();
                isPreviewing = true;

            }

            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }

        /// <summary>
        /// Webカメラが対応している解像度の取得
        /// </summary>
        /// <param name="capture"></param>
        /// <returns></returns>
        private List<VideoEncodingProperties> GetPreviewResolusions(MediaCapture capture)
        {
            IReadOnlyList<IMediaEncodingProperties> ret;
            ret = capture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);

            if (ret.Count <= 0)
            {
                return new List<VideoEncodingProperties>();
            }

            foreach (VideoEncodingProperties vp in ret)
            {
                var frameRate = (vp.FrameRate.Numerator / vp.FrameRate.Denominator);

                Debug.WriteLine("{0}: {1}x{2} {3}fps", vp.Subtype, vp.Width, vp.Height, frameRate);

            }

            return ret.Select(item => (VideoEncodingProperties)item).ToList();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btn_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                //キャプチャーイメージのクリア
                img.Source = null;

                //保存用ファイルの作成
                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync("pict.jpg", CreationCollisionOption.ReplaceExisting);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await capture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);

                //保存した画像ファイルの読み込みと表示
                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                img.Source = bitmap;

                //FaceAPIの呼び出し
                if (photoStream.Size >= 0)
                {
                    Makerequest();
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// FaceAPIにリクエストして顔を認識
        /// </summary>
        private async void Makerequest()
        {
            var client = new HttpClient();

            var requestUrl = "https://api.projectoxford.ai/face/v0/detections?analyzesFaceLandmarks=true&analyzesAge=true&analyzesGender=true&analyzesHeadPose=true&subscription-key=[your subscription-key]";
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ServiceHost));

            request.RequestUri = new Uri(requestUrl);

            //画像ファイル読み込み
            StorageFile file = await KnownFolders.PicturesLibrary.GetFileAsync("pict.jpg");
            var stream = await file.OpenReadAsync();

            //画像データをリクエストBodyに設定
            request.Content = new HttpStreamContent(stream);
            request.Content.Headers.ContentType = new HttpMediaTypeHeaderValue("application/octet-stream");

            var response = new HttpResponseMessage();

            //POSTリクエスト
            response = await client.SendRequestAsync(request);

            Debug.WriteLine(response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = null;
                if (response.Content != null)
                {
                    //Responseデータ読み込み
                    responseContent = await response.Content.ReadAsStringAsync();

                    //JSONオブジェクトに展開
                    JsonValue data = JsonValue.Parse(responseContent);

                    //年齢
                    var age = data.GetArray()[0].GetObject().GetNamedValue("attributes").GetObject().GetNamedValue("age");
                    //性別
                    var gender = data.GetArray()[0].GetObject().GetNamedValue("attributes").GetObject().GetNamedValue("gender");
                    //認識した顔の座標
                    var t = data.GetArray()[0].GetObject().GetNamedValue("faceRectangle").GetObject().GetNamedValue("top").GetNumber();
                    var l = data.GetArray()[0].GetObject().GetNamedValue("faceRectangle").GetObject().GetNamedValue("left").GetNumber();
                    var w = data.GetArray()[0].GetObject().GetNamedValue("faceRectangle").GetObject().GetNamedValue("width").GetNumber();
                    var h = data.GetArray()[0].GetObject().GetNamedValue("faceRectangle").GetObject().GetNamedValue("height").GetNumber();

                    //Debug.WriteLine("Age: {0} Gender: {1}", age.GetNumber(), gender.GetString());
                    textBox.Text = "Age: " + age.GetNumber() + "  Gender: " + gender.GetString();

                    //認識した顔の位置
                    canvas.Children.Clear();

                    Rectangle rect = new Rectangle
                    {
                        Height = h,
                        Width = w,
                        Stroke = new SolidColorBrush(Windows.UI.Colors.Red),
                        StrokeThickness=2
                    };

                    canvas.Children.Add(rect);
                    Canvas.SetLeft(rect, l);
                    Canvas.SetTop(rect, t);
                }
            }
        }

    }
    
    
}
