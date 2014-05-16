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
using AForge.Video.FFMPEG;
using System.Threading;

namespace Server
{
    public partial class Form1 : Form
    {
        private TcpListener server = null;
        private List<Image> images;

        public Form1()
        {
            InitializeComponent();
            new Thread(StartServer).Start();
        }

        private void StartServer(object obj)
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
                    //new Thread(ClientConnection).Start(client);
                    ClientConnection(client);
                    client.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Some Exception occurs: {0}", e);
            }
        }

        private void ClientConnection(object obj)
        {
            TcpClient client = (TcpClient)obj;
            NetworkStream stream = null;

            // Get a stream object for reading and writing
            stream = client.GetStream();
            stream.WriteTimeout = 15000;
            stream.ReadTimeout = 15000;

            ReadMessage(stream);
        }

        private void ReadMessage(object obj)
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
                            pictureBox.Image = data;
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
                VideoFileWriter writer = new VideoFileWriter();
                writer.Open("video.avi", 320, 240, 25, VideoCodec.MPEG4, 1000000);
                for (int i = 0; i < images.Count; i++)
                {
                    writer.WriteVideoFrame((Bitmap)images[i]);
                }
                writer.Close();
            }
        }

        public Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn, 0, byteArrayIn.Length);
            ms.Write(byteArrayIn, 0, byteArrayIn.Length);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }
    }
}
