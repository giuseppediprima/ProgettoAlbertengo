using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using SharpAvi.Output;
using SharpAvi.Codecs;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Server.MyImageDataSetTableAdapters;

namespace Server
{
    public partial class Form1 : Form
    {
        private TcpListener server = null;
        private List<Image> images;
        int width = 320;
        int height = 240;

        public Form1()
        {
            InitializeComponent();
            pictureBox.Image = pictureBox.InitialImage;

            new Thread(new ThreadStart(StartRecordingServer)).Start();

            new Thread(new ThreadStart(StartUpdateServer)).Start();
        }

        private void StartUpdateServer()
        {
            Int32 port = 14000;
            IPAddress address = IPAddress.Any;
            try
            {
                server = new TcpListener(address, port);
                server.Start();

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        textBox1.Text = "Upload Images....";
                        linkLabel1.Enabled = false;
                        linkLabel1.Links.Clear();
                        linkLabel1.Text = "";
                    });
                    ClientConnection(client, false);
                    client.Close();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox.Image = pictureBox.InitialImage;
                        textBox1.Text = "Waiting for Device...";
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Some Exception occurs: {0}", e);
            }
        }

        private void StartRecordingServer()
        {
            Int32 port = 13000;
            IPAddress address = IPAddress.Any;
            try
            {
                server = new TcpListener(address, port);
                server.Start();

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    this.Invoke((MethodInvoker) delegate()
                    {
                        textBox1.Text = "Recording....";
                        linkLabel1.Enabled = false;
                        linkLabel1.Links.Clear();
                        linkLabel1.Text = "";
                    });
                    ClientConnection(client, true);
                    client.Close();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox.Image = pictureBox.InitialImage;
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Some Exception occurs: {0}", e);
            }
        }

        private void ClientConnection(object obj, bool streaming)
        {
            TcpClient client = (TcpClient)obj;
            NetworkStream stream = null;

            // Get a stream object for reading and writing
            stream = client.GetStream();
            stream.WriteTimeout = 15000;
            stream.ReadTimeout = 15000;

            if (streaming)
                StreamingVideo(stream);
            else
                UploadImages(stream);
        }

        private void UploadImages(Object obj)
        {
            images = new List<Image>();
            // Buffer for reading data
            Byte[] bytes = new Byte[230454];
            Image data = null;
            NetworkStream stream = (NetworkStream)obj;

            int totalByteRead = 0;
            int bytesRead;
            try
            {
                byte[] num_images = new byte[1];

                if (stream.Read(num_images, 0, 1) == 0)
                {
                    throw new Exception();
                }

                if (num_images[0] == 0)
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        MessageBox.Show("Nessuna Immagine da memorizzare...");
                        textBox1.Text = "Waiting for Device...";
                    });
                    return;
                }
                
                int images_read = 0;
                while ((bytesRead = stream.Read(bytes, totalByteRead, bytes.Length - totalByteRead)) != 0)
                {
                    totalByteRead += bytesRead;

                    if (totalByteRead == 230454)
                    {
                        Console.WriteLine("Received Image...");
                        data = byteArrayToImage(bytes);
                        totalByteRead = 0;
                        this.Invoke((MethodInvoker)delegate()
                        {
                            pictureBox.Image = (Image)data.Clone();
                        });
                        images.Add(data);
                        if (++images_read == num_images[0])
                            break;
                    }
                }

                //Save images in DB
                //TODO sostituire con una transazione SQL
                ImagesTableAdapter ita = new ImagesTableAdapter();
                byte[] msg = new byte[1];
                msg[0] = 0;
                for (int i = images_read-1; i >= 0; i--)
                {
                    //TODO controllare se l'operazione di inserimento va a buon fine
                    ita.Insert(imageToByteArray(images[i]));
                }

                //Send deleting message to device
                // msg = 0 <=> DELETE (ALL OK)
                // msg = 1 <=> ERROR
                stream.Write(msg, 0, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine("Eccezione: " + e.StackTrace);
            }
        }

        private void StreamingVideo(object obj)
        {
            images = new List<Image>();
            // Buffer for reading data
            Byte[] bytes = new Byte[230454];
            Image data = null;
            NetworkStream stream = (NetworkStream)obj;

            int totalByteRead = 0;
            int bytesRead;
            try
            {
                while ((bytesRead = stream.Read(bytes, totalByteRead, bytes.Length - totalByteRead)) != 0)
                {
                    totalByteRead += bytesRead;

                    if (totalByteRead == 230454)
                    {
                        Console.WriteLine("Received Image...");
                        data = byteArrayToImage(bytes);
                        totalByteRead = 0;
                        this.Invoke((MethodInvoker)delegate()
                        {
                            pictureBox.Image = (Image)data.Clone();
                        });
                        images.Add(data);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Eccezione: " + e.StackTrace);
            }
            finally
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    textBox1.Text = "Creating Video File...";
                });
                AviWriter writer = new AviWriter("prova.avi");
                writer.FramesPerSecond = 1;
                IVideoEncoder encoder = new RgbVideoEncoder(width, height);
                AsyncVideoStreamWrapper videoStream = writer.AddVideoStream().WithEncoder(encoder).Async();
                videoStream.Name = "MyVideoStream";
                videoStream.Width = width;
                videoStream.Height = height;
                for (int i = 0; i < images.Count; i++) {
                    byte[] image = new byte[width * height * 4];
                    GetByteArray((Bitmap)images[i], image);
                    Console.WriteLine("Image size: " + image.Length);
                    videoStream.BeginWriteFrame(true, image, 0, image.Length);
                    videoStream.EndWriteFrame();
                }
                writer.Close();
                this.Invoke((MethodInvoker)delegate()
                {
                    textBox1.Text = "Waiting for Device...";
                    linkLabel1.Enabled = true;
                    linkLabel1.Text = "Visualizza il Video o invialo per E-Mail...";
                    //linkLabel1.LinkArea = new System.Windows.Forms.LinkArea(14, 5);
                    linkLabel1.Links.Add(14, 5, "video");
                    linkLabel1.Links.Add(34, 6, "email");
                });
            }
        }

        public Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn, 0, byteArrayIn.Length);
            ms.Write(byteArrayIn, 0, byteArrayIn.Length);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }

        public byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            MemoryStream ms = new MemoryStream();
            imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp );
            return ms.ToArray();
        }

        private void GetByteArray(Bitmap image, byte[] buffer)
        {
            using (var bitmap = image)
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
                bitmap.UnlockBits(bits);
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Determine which link was clicked within the LinkLabel. 
            this.linkLabel1.Links[linkLabel1.Links.IndexOf(e.Link)].Visited = true;

            // Display the appropriate link based on the value of the  
            // LinkData property of the Link object. 
            string target = e.Link.LinkData as string;

            if (target != null && target.Equals("video"))
                System.Diagnostics.Process.Start("prova.avi");
            else if (target != null && target.Equals("email"))
            {
                Thread t = new Thread(new ThreadStart(() => { 
                    Application.Run(new frmTestEmail("progettoalbertengo@gmail.com", "Prova Video Send", "Watch my Video!!!!", Path.GetDirectoryName(Application.ExecutablePath)+"\\prova.avi")); 
                }));
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
        }
    }
}
