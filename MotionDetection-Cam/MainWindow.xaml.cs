using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//using System.Windows.Shapes;
using System.Drawing;
using Microsoft.Kinect;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO;


using System.Collections.Generic;
using System.Diagnostics;
using Emgu.CV.GPU;

namespace KinectWPFOpenCV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor;
        WriteableBitmap depthBitmap;
        WriteableBitmap colorBitmap;
        DepthImagePixel[] depthPixels;
        byte[] colorPixels;

        int blobCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.MouseDown += MainWindow_MouseDown;

        }


        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {

                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.depthBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorImg.Source = this.colorBitmap;

                this.sensor.AllFramesReady += this.sensor_AllFramesReady;


                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.outputViewbox.Visibility = System.Windows.Visibility.Collapsed;
                this.txtError.Visibility = System.Windows.Visibility.Visible;
                this.txtInfo.Text = "No Kinect Found";

            }

        }


        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            BitmapSource depthBmp = null;
            blobCount = 0;

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null)
                    {

                        blobCount = 0;

                        depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);

                        Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                        Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();
                        Image<Bgr, byte> color = new Image<Bgr, byte>(depthBmp.ToBitmap());



                       using (MemStorage stor = new MemStorage())
                        {
                            //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                            Contour<System.Drawing.Point> contours = gray_image.FindContours(
                             Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                             Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                             stor);

                            //Contour<System.Drawing.Point> contours  = gray_image.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.
                             //     CV_CHAIN_APPROX_SIMPLE,  Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE, stor);

                            for (int i = 0; contours != null; contours = contours.HNext)
                            {
                                i++;

                                ////////////////////////////////
                                // Contour<System.Drawing.Point> currentContour = contours.ApproxPoly(contours.Perimeter * 0.015, stor);
                                // if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSize.Value, 2))
                                //     && currentContour.BoundingRectangle.Width > 20)
                               //  {
                              //       CvInvoke.cvDrawContours(color, contours, new MCvScalar(255), new MCvScalar(255), -1, 2,
                                  //           Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                                //         new System.Drawing.Point(0, 0));
                               //      openCVImg.Draw(currentContour.BoundingRectangle, new Bgr(0, 255, 0), 1);
                               //      blobCount++;
                               //  }

                            

                                if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && 
                                    (contours.Area < Math.Pow(sliderMaxSize.Value, 2)))
                                {
                                    MCvBox2D box = contours.GetMinAreaRect();
                                    openCVImg.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                                    blobCount++;
                                }
                            }
                        }

                        this.outImg.Source = ImageHelpers.ToBitmapSource(openCVImg);
                        txtBlobCount.Text = blobCount.ToString();
                    }

                if (colorFrame != null)
                {

                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);

                }

            }
        }
    }

    /// <summary>
    /// Bala
    /// </summary>
    /// <param name="image"></param>
    /// <param name="processingTime"></param>
    /// <returns></returns>
    public Image<Bgr, Byte> Find(Image<Bgr, Byte> image, out long processingTime)
    {
        Stopwatch watch;
        Rectangle[] regions;

        //check if there is a compatible GPU to run pedestrian detection
        if (GpuInvoke.HasCuda)
        {  //this is the GPU version
            using (GpuHOGDescriptor des = new GpuHOGDescriptor())
            {
                des.SetSVMDetector(GpuHOGDescriptor.GetDefaultPeopleDetector());

                watch = Stopwatch.StartNew();
                using (GpuImage<Bgr, Byte> gpuImg = new GpuImage<Bgr, byte>(image))
                using (GpuImage<Bgra, Byte> gpuBgra = gpuImg.Convert<Bgra, Byte>())
                {
                    regions = des.DetectMultiScale(gpuBgra);
                }
            }
        }
        else
        {  //this is the CPU version
            using (HOGDescriptor des = new HOGDescriptor())
            {
                des.SetSVMDetector(HOGDescriptor.GetDefaultPeopleDetector());

                watch = Stopwatch.StartNew();
                regions = des.DetectMultiScale(image);
            }
        }
        watch.Stop();

        processingTime = watch.ElapsedMilliseconds;

        foreach (Rectangle pedestrain in regions)
        {
            image.Draw(pedestrain, new Bgr(System.Drawing.Color.Red), 1);
        }

        return image;
    }


    /// <summary>
    /// /////////////////////
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    #region Window Stuff
    void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }


    void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (null != this.sensor)
        {
            this.sensor.Stop();
        }
    }

    private void CloseBtnClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    #endregion
}
}
