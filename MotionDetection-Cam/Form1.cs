using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.VideoSurveillance;
using Emgu.Util;

namespace MotionDetection
{
    public partial class Form1 : Form
    {

        private Capture _capture;
        private MotionHistory _motionHistory;
        private IBGFGDetector<Bgr> _forgroundDetector;
        private int uiArea = 0;
        Dictionary<int, List<int>> movement = null;
        int identifiedMovement = 0;
        int motionTextUpdate = 0;
        int rightMovement = 0;
        int leftMovement = 0;
       // Boolean objectDetected = false;
       // Dictionary<int, List<int>> compStatus;

        List<int> singleObjTracking = new List<int>();

        public Form1()
        {
            InitializeComponent();

            //try to create the capture
            if (_capture == null)
            {
               // compStatus = new Dictionary<int, List<int>>();
                try
                {
                    _capture = new Capture();
                }
                catch (NullReferenceException excpt)
                {   //show errors if there is any
                    MessageBox.Show(excpt.Message);
                }
            }

            if (_capture != null) //if camera capture has been successfully created
            {
                _motionHistory = new MotionHistory(
                    1.0, //in second, the duration of motion history you wants to keep
                    0.05, //in second, maxDelta for cvCalcMotionGradient
                    0.5); //in second, minDelta for cvCalcMotionGradient

                _capture.ImageGrabbed += ProcessFrame;
                _capture.Start();
            }
        }

        private void trackBarArea_Scroll(object sender, EventArgs e)
        {
            lblArea.Text = trackBarArea.Value.ToString();

        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            using (Image<Bgr, Byte> image = _capture.RetrieveBgrFrame())
            using (MemStorage storage = new MemStorage()) //create storage for motion components
            {
                image._SmoothGaussian(3); //filter out noises

                if (_forgroundDetector == null)
                {
                    //_forgroundDetector = new BGCodeBookModel<Bgr>();
                    _forgroundDetector = new FGDetector<Bgr>(Emgu.CV.CvEnum.FORGROUND_DETECTOR_TYPE.FGD);
                    //_forgroundDetector = new BGStatModel<Bgr>(image, Emgu.CV.CvEnum.BG_STAT_TYPE.FGD_STAT_MODEL);
                }

                _forgroundDetector.Update(image);
                capturedImageBox.Image = image;

                //update the motion history
                _motionHistory.Update(_forgroundDetector.ForgroundMask);

                //forgroundImageBox.Image = _forgroundDetector.ForgroundMask;

                #region get a copy of the motion mask and enhance its color
                double[] minValues, maxValues;
                Point[] minLoc, maxLoc;
                _motionHistory.Mask.MinMax(out minValues, out maxValues, out minLoc, out maxLoc);
                Image<Gray, Byte> motionMask = _motionHistory.Mask.Mul(255.0 / maxValues[0]);
                #endregion


                //create the motion image 
                Image<Bgr, Byte> motionImage = new Image<Bgr, byte>(motionMask.Size);
                //display the motion pixels in blue (first channel)
                motionImage[0] = motionMask;

                //Threshold to define a motion area, reduce the value to detect smaller motion
                // double minArea = 5000;

                getArea();
                int minArea = uiArea;

                storage.Clear(); //clear the storage
                Seq<MCvConnectedComp> motionComponents = _motionHistory.GetMotionComponents(storage);

                identifiedMovement = 0;

                movement = new Dictionary<int, List<int>>();

                
              /*  // Left direction
                compStatus.Add(1,null);
                // Right direction
                compStatus.Add(2, null);
                // 
                compStatus.Add(3,null);*/

                //iterate through each of the motion component
                foreach (MCvConnectedComp comp in motionComponents)
                {                   
                    //reject the components that have small area;
                    if (comp.area < minArea) continue;

                    // find the angle and motion pixel count of the specific area
                    double angle, motionPixelCount;
                    _motionHistory.MotionInfo(comp.rect, out angle, out motionPixelCount);
                                        
                    //reject the area that contains too few motion
                    if (motionPixelCount < comp.area * 0.05) continue;

                    //Draw each individual motion in red
                    int xDirection = DrawMotion(motionImage, comp.rect, angle, new Bgr(Color.Red), singleObjTracking);
                    identifiedMovement++;
                }

                motionTextUpdate++;
                // find and draw the overall motion angle
                double overallAngle, overallMotionPixelCount;
                _motionHistory.MotionInfo(motionMask.ROI, out overallAngle, out overallMotionPixelCount);

                if (singleObjTracking.Count > 12)
                {
                    //if(singleObjTracking.FindIndex(x => x < 10) < singleObjTracking.FindIndex(x => x > 150))
                    if (singleObjTracking[0] < 10 &&  singleObjTracking[singleObjTracking.Count -1] > 160)
                    {
                        singleObjTracking = new List<int>();
                        rightMovement++;
                        startUpdatingData = false;
                    }
                    //else if (singleObjTracking.FindIndex(x => x < 10) > singleObjTracking.FindIndex(x => x > 150))
                    else if (singleObjTracking[0] > 160 &&  singleObjTracking[singleObjTracking.Count - 1] < 10)
                    {
                        singleObjTracking = new List<int>();
                        leftMovement++;
                        startUpdatingData = false;
                    }
                }else if (singleObjTracking.Count > 30)
                {
                    singleObjTracking = new List<int>();
                    startUpdatingData = false;
                }

                    UpdateText(String.Format("\n Number of detected motion towards IN => {0} and OUT => {1} ", leftMovement,rightMovement));

                //DrawMotion(motionImage, motionMask.ROI, overallAngle, new Bgr(Color.Green));               

                /*   if (motionTextUpdate % 5 == 0)
                   {
                       UpdateText(String.Format("\n Number of detected motion towards left => {0} and right => {1} ", compStatus[1], compStatus[2]));
                   }*/


                //  UpdateLabel2(String.Format("Total Motions found: {0}", identifiedMovement));


                //Display the image of the motion
                motionImageBox.Image = motionImage;
            }
        }

        private Boolean startUpdatingData = false;
        private int DrawMotion(Image<Bgr, Byte> image, Rectangle motionRegion, double angle, Bgr color, /* Dictionary<int, List<int>> compStatus*/
            List<int> singleObjTracking)
        {

            float circleRadius = (motionRegion.Width + motionRegion.Height) >> 2;
            Point center = new Point(motionRegion.X + motionRegion.Width >> 1,
                motionRegion.Y + motionRegion.Height >> 1);

            CircleF circle = new CircleF(
               center,
               circleRadius);

            int xDirection = (int)(Math.Cos(angle * (Math.PI / 180.0)) * circleRadius);
            int yDirection = (int)(Math.Sin(angle * (Math.PI / 180.0)) * circleRadius);
            Point pointOnCircle = new Point(
                center.X + xDirection,
                center.Y - yDirection);

            LineSegment2D line = new LineSegment2D(center, pointOnCircle); 

            image.Draw(circle, color, 1);
            image.Draw(line, color, 2);

            if (singleObjTracking.Count <5 &&  (motionRegion.Location.X <10 || motionRegion.Location.X > 150))
            {
                startUpdatingData = true;                
            }

            if(startUpdatingData == true)
            {
                singleObjTracking.Add(motionRegion.Location.X);
            }
           
            UpdateTextBox(String.Format("\n Detected  -> {0} & count => {1} ", motionRegion.Location.X, singleObjTracking.Count));

          /*  if (motionRegion.X < xDirection)
            {
                //   int val = 1;
                if (compStatus.ContainsKey(1))
                {
                    compStatus[1].Add(xDirection);

                  // val = val + compStatus[1];
                }
               // compStatus[1] = val;
                // UpdateTextBox(String.Format("\n X Direction -> {0} ", motionRegion.X));
                //     UpdateTextBox(String.Format("\n Detected motion from left to right!!"));
            }
            else
            {
               // int val = 1;
                if (compStatus.ContainsKey(2))
                {
                    compStatus[2].Add(xDirection);
                    // val = val + compStatus[2];
                }
              //  compStatus[2] = val;
                //   UpdateTextBox(String.Format("\n Detected motion from right to left!!"));
            }*/


            

            return xDirection;
        }

        private void UpdateArea(string value)
        {
            if (InvokeRequired && !IsDisposed)
            {
                Invoke((Action<string>)UpdateText, value);
            }
            else
            {
                lblArea.Text = value;
            }
        }

        public void getArea()
        {
            if (InvokeRequired)
            {
                // Invoke this method on the UI thread using an anonymous delegate
                Invoke(new MethodInvoker(() => getArea()));
                return ;
            }

            string val = trackBarArea.Value.ToString();
            UpdateArea(val);
            uiArea =  trackBarArea.Value;
        }

        private void UpdateText(String text)
        {
            if (InvokeRequired && !IsDisposed)
            {
                Invoke((Action<string>)UpdateText, text);
            }
            else
            {
                label3.Text = text;
            }
        }

        private void UpdateLabel2(String text)
        {
            if (InvokeRequired && !IsDisposed)
            {
                Invoke((Action<string>)UpdateText, text);
            }
            else
            {
                //label2.Text = text;
            }
        }
        private void UpdateTextBox(String text)
        {
            if (InvokeRequired && !IsDisposed)
            {
                Invoke((Action<string>)UpdateTextBox, text);
            }
            else
            {
                // textBox += System.Environment.NewLine + text;
                textBox.AppendText(text + Environment.NewLine);
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {

            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _capture.Stop();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
