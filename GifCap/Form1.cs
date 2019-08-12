using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace GifCap
{
    public partial class Form1 : Form
    {

        public int FramesToRunFor = 150;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Start capture loop when the winform loads
            Thread CaptureLoopThread = new Thread(CaptureLoop);
            CaptureLoopThread.Start();
        }

        public void CaptureLoop()
        {
            //Check if the frames folder alreay exists and delete it's contents
            if(Directory.Exists("Frames"))
            {
                foreach (String file in System.IO.Directory.GetFiles("Frames"))
                {
                    FileInfo f = new FileInfo(file);
                    File.Delete(f.FullName);
                }
            }
            else
            {
                System.IO.Directory.CreateDirectory("Frames");
            }

            //Every 0.3 seconds capture a frame.
            for (int i = 0; i < FramesToRunFor; i++)
            {
                Thread CaptureFrameThread = new Thread(CaptureFrame);
                CaptureFrameThread.Start(i);
                Console.WriteLine("Frame " + i + " / " + FramesToRunFor);
                Thread.Sleep(1000 / 30);
            }
            //Build gif from captured frames.
            Thread BuildGifThread = new Thread(FramesToGif);
            BuildGifThread.Start();
            Console.WriteLine("Rendering gif...");
        }

        //Capture a frame
        Rectangle bounds = Screen.GetBounds(Point.Empty);
        public void CaptureFrame(object FrameNumD)
        {
            //Convert the object parameter to an int for the file name.
            int FrameNum = (int)FrameNumD;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                //Capture the entire virtual monitor
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }
                //Save file and free up memory
                bitmap.Save("Frames\\" + FrameNum.ToString().PadLeft(3, '0') + ".png", ImageFormat.Png);
                bitmap.Dispose();
            }
        }

        public void FramesToGif()
        {
            //Create new encoder and add each file in the frames directory as a frame.
            GifBitmapEncoder encoder = new GifBitmapEncoder();
            Console.WriteLine("Adding frames...");
            foreach (String file in System.IO.Directory.GetFiles("Frames"))
            {
                FileInfo f = new FileInfo(file);
                encoder.Frames.Add(BitmapFrame.Create(new Uri(f.FullName)));
            }
            //Save gif to a temporary array so the application extension block data can be interspliced later.
            MemoryStream TempStream = new MemoryStream();
            encoder.Save(TempStream);
            //Create new gif file
            string GifFileName = DateTime.Now.ToString("dd-MM-yy_hh-mm-ss") + ".gif";
            FileStream GifFileStream = new FileStream(GifFileName, FileMode.Create);
            //Write the gif header and local screen discriptor to the file.
            Console.WriteLine("Writing header...");
            byte[] TransferBuffer = TempStream.ToArray();
            for (int i = 0; i < 13; i++)
            {
                GifFileStream.WriteByte(TransferBuffer[GifFileStream.Position]);
            }
            //Should always be 19 bytes, needed to make gif loop.
            Console.WriteLine("Adding animation patch...");
            byte[] ApplicationExtensionBlockData = {0x21, 0xFF, 0x0B, 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, 0x03, 0x01, 0x00, 0x00, 0x00};
            //Write the application extension block to the file.
            for (int i = 0; i < 19; i++)
            {
                GifFileStream.WriteByte(ApplicationExtensionBlockData[i]);
            }

            //Write the rest of the temp array to the file.
            Console.WriteLine("Writing body...");
            for (int i = 13; i < TempStream.Length; i++)
            {
                if(TransferBuffer[i] == 0x21 && TransferBuffer[i+1] == 0xF9 && TransferBuffer[i + 2] == 0x04 && TransferBuffer[i + 7] == 0x00)
                {
                    Console.WriteLine("Patching animation block...");
                    TransferBuffer[i + 4] = 0x03;
                }
                GifFileStream.WriteByte(TransferBuffer[i]);
            }
            //Close streams
            GifFileStream.Close();
            TempStream.Close();
            //Open file in explorer
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase) + "\\" + GifFileName + "\"");
            //Exit app
            Application.Exit();
        }

    }
}
