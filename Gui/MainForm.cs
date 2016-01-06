using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using GenArt.AST;
using GenArt.Classes;

namespace GenArt
{
    public partial class MainForm : Form
    {
        public static Settings Settings;
        private DnaDrawing currentDrawing;
        public Bitmap ForSaving;
        private double errorLevel = double.MaxValue;
        private int generation;
        private DnaDrawing guiDrawing;
        private bool isRunning;
        private DateTime lastRepaint = DateTime.MinValue;
        private int lastSelected;
        private TimeSpan repaintIntervall = new TimeSpan(0, 0, 0, 1, 0);
        private int repaintOnSelectedSteps = 4;
        private int selected;
        private SettingsForm settingsForm;
        private Color[,] sourceColors;
        private Thread thread;

        private double temperature = 100000.0;
        private double cooling = 0.001;
        private double acceptable = 0.999999999;

        public MainForm()
        {
            Console.WriteLine("Start");
            InitializeComponent(); // Kutsub esile samas klassis, kui disainile suunatud osas meetodi, mis paneb kõik formid püsti. 
            Settings = Serializer.DeserializeSettings(); // Serialize roger-i poolt kirj. klass, Dezerializib (s.t hangib) xml failist settingud, kui need olemas on. Kui seda ei eksisteeri, returnib nulli
            if (Settings == null)
                Settings = new Settings(); // Loob uue settings objekti, ei loo ise xml faili 
        }

        private void Form1_Load(object sender, EventArgs e)
        {


        }

        private static DnaDrawing GetNewInitializedDrawing()
        {
            var drawing = new DnaDrawing();
            drawing.Init();  // HAKKAB for loobis Settings.cs-is sätestatud minimaalselt arvu Polügoone lisama
            return drawing;
        }



        private void StartEvolution()
        {
            SetupSourceColorMatrix();// Sets up the Mona lisa using x and y cordinates to create an matrix. 
            if (currentDrawing == null)
                currentDrawing = GetNewInitializedDrawing();
            lastSelected = 0;

            while (isRunning)
            {
                DnaDrawing newDrawing;
                lock (currentDrawing)
                {
                    newDrawing = currentDrawing.Clone();
                }
                newDrawing.Mutate();// Multiple levels: With probability x either adds, removes or moves polygons. Next level changes polygons shape, color
                if (newDrawing.IsDirty)
                {
                    generation++;
                    double newErrorLevel = FitnessCalculator.GetDrawingFitness(newDrawing, sourceColors);

                    if (newErrorLevel <= errorLevel)
                    {
                        selected++;
                        lock (currentDrawing)
                        {
                            currentDrawing = newDrawing;
                        }
                        errorLevel = newErrorLevel;
                        if (selected%500 == 0)
                        {
                            string local_path_name= "C:\\Users\\raunonaksi\\OneDrive\\Advanced Algorithmics - MTAT.03.238\\Project\\Pildid\\";
                            string filename = "result";
                            string dot_filetype = ".png";
                            FitnessCalculator.b.Save(local_path_name + filename + generation.ToString() + dot_filetype, System.Drawing.Imaging.ImageFormat.Png);
                            FitnessCalculator.b.Save("C:\\Users\\raunonaksi\\result.png");
                    }

                    }
                    else if (Settings.Activetemperature>1 && newErrorLevel * Settings.Activeacceptable < errorLevel && Settings.UseSimulatedAnnealing)
                    {
                        Settings.Activetemperature = Settings.Activetemperature - Settings.Activetemperature * Settings.Activecooling;
                        if (Tools.GetRandomNumber(0, 100000) < Settings.Activetemperature)
                        {
                            currentDrawing = newDrawing;
                            errorLevel = newErrorLevel;
                        }
                    }
                }
   
               
            }
        }

        //covnerts the source image to a Color[,] for faster lookup
        private void SetupSourceColorMatrix()
        {

            sourceColors = new Color[Tools.MaxWidth,Tools.MaxHeight];
            var sourceImage = picPattern.Image as Bitmap; 

            if (sourceImage == null)
                throw new NotSupportedException("A source image of Bitmap format must be provided");

            for (int y = 0; y < Tools.MaxHeight; y++)
            {
                for (int x = 0; x < Tools.MaxWidth; x++)
                {
                    Color c = sourceImage.GetPixel(x, y);
                    sourceColors[x, y] = c;
                }
            }
        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            if (isRunning)
                Stop();
            else
                Start();
        }

        private void Start()
        {
            btnStart.Text = "Stop";
            isRunning = true;
            tmrRedraw.Enabled = true;

            if (thread != null)
                KillThread();

            thread = new Thread(StartEvolution)
                         {
                             IsBackground = true,
                             Priority = ThreadPriority.AboveNormal
                         };

            thread.Start();
        }

        private void KillThread()
        {
            if (thread != null)
            {
                thread.Abort();
            }
            thread = null;
        }

        private void Stop()
        {
            if (isRunning)
                KillThread();

            btnStart.Text = "Start";
            isRunning = false;
            tmrRedraw.Enabled = false;
        }

        private void tmrRedraw_Tick(object sender, EventArgs e)
        {
            if (currentDrawing == null)
                return;

            int polygons = currentDrawing.Polygons.Count;
            int points = currentDrawing.PointCount;
            double avg = 0;
            if (polygons != 0)
                avg = points/polygons;

            toolStripStatusLabelFitness.Text = errorLevel.ToString();
            toolStripStatusLabelGeneration.Text = generation.ToString();
            toolStripStatusLabelSelected.Text = selected.ToString();
            toolStripStatusLabelPoints.Text = points.ToString();
            toolStripStatusLabelPolygons.Text = polygons.ToString();
            toolStripStatusLabelAvgPoints.Text = avg.ToString();

            bool shouldRepaint = false;
            if (repaintIntervall.Ticks > 0)
                if (lastRepaint < DateTime.Now - repaintIntervall)
                    shouldRepaint = true;

            if (repaintOnSelectedSteps > 0)
                if (lastSelected + repaintOnSelectedSteps < selected)
                    shouldRepaint = true;

            if (shouldRepaint)
            {
                lock (currentDrawing)
                {
                    guiDrawing = currentDrawing.Clone();
                }
                pnlCanvas.Invalidate();
                lastRepaint = DateTime.Now;
                lastSelected = selected;
            }
        }

        private void pnlCanvas_Paint(object sender, PaintEventArgs e)
        {
            if (guiDrawing == null)
            {
                e.Graphics.Clear(Color.Black);
                return;
            }


            using (
                var backBuffer = new Bitmap(trackBarScale.Value*picPattern.Width, trackBarScale.Value*picPattern.Height,
                                            PixelFormat.Format24bppRgb))
            using (Graphics backGraphics = Graphics.FromImage(backBuffer))
            {
                backGraphics.SmoothingMode = SmoothingMode.HighQuality;
                Renderer.Render(guiDrawing, backGraphics, trackBarScale.Value);

                e.Graphics.DrawImage(backBuffer, 0, 0);
            }
        }

        private void OpenImage()
        {
            Stop();

            string fileName = FileUtil.GetOpenFileName(FileUtil.ImgExtension);
            if (string.IsNullOrEmpty(fileName))
                return;

            picPattern.Image = Image.FromFile(fileName);

            Tools.MaxHeight = picPattern.Height;
            Tools.MaxWidth = picPattern.Width;

            SetCanvasSize();

            splitContainer1.SplitterDistance = picPattern.Width + 30;
        }

        private void SetCanvasSize()
        {
            pnlCanvas.Height = trackBarScale.Value*picPattern.Height;
            pnlCanvas.Width = trackBarScale.Value*picPattern.Width;
        }

        private void OpenDNA()
        {
            Stop();

            DnaDrawing drawing = Serializer.DeserializeDnaDrawing(FileUtil.GetOpenFileName(FileUtil.DnaExtension));
            if (drawing != null)
            {
                if (currentDrawing == null)
                    currentDrawing = GetNewInitializedDrawing();

                lock (currentDrawing)
                {
                    currentDrawing = drawing;
                    guiDrawing = currentDrawing.Clone();
                }
                pnlCanvas.Invalidate();
                lastRepaint = DateTime.Now;
            }
        }

        private void SaveDNA()
        {
            string fileName = FileUtil.GetSaveFileName(FileUtil.DnaExtension);
            if (string.IsNullOrEmpty(fileName) == false && currentDrawing != null)
            {
                DnaDrawing clone = null;
                lock (currentDrawing)
                {
                    clone = currentDrawing.Clone();
                }
                if (clone != null)
                    Serializer.Serialize(clone, fileName);
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (settingsForm != null)
                if (settingsForm.IsDisposed)
                    settingsForm = null;

            if (settingsForm == null)
                settingsForm = new SettingsForm();

            settingsForm.Show();
        }

        private void sourceImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenImage();
        }

        private void dNAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDNA();
        }

        private void dNAToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveDNA();
        }

        private void trackBarScale_Scroll(object sender, EventArgs e)
        {
            SetCanvasSize();
        }
    }
}