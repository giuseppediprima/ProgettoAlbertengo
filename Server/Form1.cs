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
using System.Transactions;
using SharpAvi.Output;
using SharpAvi.Codecs;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Server.MyImageDataSetTableAdapters;
using System.Data.SqlClient;

namespace Server
{
    public partial class Form1 : Form
    {
        
        private List<Image> images;
        private ImageList listaFirstFrame;
        int width = 320;
        int height = 240;

        public Form1()
        {
            InitializeComponent();
            pictureBox1.Image = pictureBox1.InitialImage;
            listaFirstFrame = new ImageList();
            

            new Thread(new ThreadStart(StartRecordingServer)).Start();

            new Thread(new ThreadStart(StartUpdateServer)).Start();
        }

        private void StartUpdateServer()
        {
            Int32 port = 14000;
            IPAddress address = IPAddress.Any;
            try
            {
                TcpListener server = new TcpListener(address, port);
                server.Start();

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        label2.Text = "Upload Images...";
                       
                        
                    });
                    ClientConnection(client, false);
                    client.Close();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox1.Image = pictureBox1.InitialImage;
                        label2.Text = "Waiting for Device...";
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
                TcpListener  server = new TcpListener(address, port);
                server.Start();

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    this.Invoke((MethodInvoker) delegate()
                    {
                        label2.Text = "Recording....";
                        
                    });
                    ClientConnection(client, true);
                    client.Close();
                    this.Invoke((MethodInvoker)delegate()
                    {
                        pictureBox1.Image = pictureBox1.InitialImage;
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
            //Send deleting message to device
            // msg = 0 <=> DELETE (ALL OK)
            // msg = 1 <=> ERROR
            byte[] msg = new byte[1];
            msg[0] = 0;

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
                        label2.Text = "Waiting for Device...";
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
                            pictureBox1.Image = (Image)data.Clone();
                        });
                        images.Add(data);
                        if (++images_read == num_images[0])
                            break;
                    }
                }

                //Save images in DB
               

                using (SqlConnection connection = new SqlConnection("Data Source=WINMAC-PC\\SQLEXPRESS;Initial Catalog=MyDB;Integrated Security=True"))
                {
                    connection.Open();

                    SqlCommand command = connection.CreateCommand();
                    SqlTransaction transaction;

                    // Start a local transaction.
                    transaction = connection.BeginTransaction("Transaction");

                    // Must assign both transaction object and connection 
                    // to Command object for a pending local transaction
                    command.Connection = connection;
                    command.Transaction = transaction;

                    try
                    {
                        for (int i = images_read - 1; i >= 0; i--)
                        {
                            command.CommandText = "Insert into Images (image) VALUES (@image)";
                            byte[] img = imageToByteArray(images[i]);
                            command.Parameters.Add("@image", SqlDbType.VarBinary, img.Length).Value = img;
                            command.ExecuteNonQuery();
                        }

                        // Attempt to commit the transaction.
                        transaction.Commit();
                        Console.WriteLine("Both records are written to database.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                        Console.WriteLine("  Message: {0}", ex.Message);

                        // Attempt to roll back the transaction. 
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception ex2)
                        {
                            // This catch block will handle any errors that may have occurred 
                            // on the server that would cause the rollback to fail, such as 
                            // a closed connection.
                            Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                            Console.WriteLine("  Message: {0}", ex2.Message);
                        }
                        msg[0] = 1;
                        stream.Write(msg, 0, 1);
                        return;
                    }
                  
                }

                stream.Write(msg, 0, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine("Eccezione: " + e.StackTrace);
                msg[0] = 1;
                stream.Write(msg, 0, 1);
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
                            pictureBox1.Image = (Image)data.Clone();
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
                    label2.Text = "Creating Video File...";
                });

                try
                {
                    Image tmpImage = (Image)images[0].Clone();
                    String path=GetNextFileName();
                    AviWriter writer = new AviWriter(path);
                    writer.FramesPerSecond = 1;
                    IVideoEncoder encoder = new RgbVideoEncoder(width, height);
                    AsyncVideoStreamWrapper videoStream = writer.AddVideoStream().WithEncoder(encoder).Async();
                    videoStream.Name = "MyVideoStream";
                    videoStream.Width = width;
                    videoStream.Height = height;
                    for (int i = 0; i < images.Count; i++)
                    {
                        byte[] image = new byte[width * height * 4];
                        GetByteArray((Bitmap)images[i], image);
                        Console.WriteLine("Image size: " + image.Length);
                        videoStream.BeginWriteFrame(true, image, 0, image.Length);
                        videoStream.EndWriteFrame();
                    }
                    writer.Close();
                    tmpImage.Save(path.Replace("avi","bmp"), ImageFormat.Bmp);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    this.Invoke((MethodInvoker)delegate()
                    {
                        label2.Text = "Error Saving Video...";

                    });

                    Thread.Sleep(1000);
                }
                finally
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        label2.Text = "Waiting for Device...";

                    });
                }

            }
        }

        private string GetNextFileName()
        {
            String fileName = DateTime.Now.ToString(@"dd-MM-yyyy_HH.mm.ss")+".avi";
            try
            {
                if (!Directory.Exists("./Video/"))
                {

                    Directory.CreateDirectory("./Video/");
                }
            }
            catch (Exception e)
            {
                return "./"+fileName;
            }

            return "./Video/" + fileName;
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

        private void btnCancel_Click(object sender, EventArgs e)
        {
            txtSendTo.Text = "";
            txtMessage.Text = "";
            txtSubjectLine.Text = "";
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(txtSendTo.Text))
            {
                MessageBox.Show("Missing recipient address.", "Email Error");
                return;
            }

            if (String.IsNullOrEmpty(txtSendFrom.Text))
            {
                MessageBox.Show("Missing sender address.", "Email Error");
                return;
            }

            if (String.IsNullOrEmpty(txtSubjectLine.Text))
            {
                MessageBox.Show("Missing subject line.", "Email Error");
                return;
            }

            if (String.IsNullOrEmpty(txtMessage.Text))
            {
                MessageBox.Show("Missing message.", "Email Error");
                return;
            }

            
            /*
            string[] arr = txtAttachments.Text.Split(';');
            alAttachments = new ArrayList();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!String.IsNullOrEmpty(arr[i].ToString().Trim()))
                {
                    alAttachments.Add(arr[i].ToString().Trim());
                }
            }

            this.Hide();
            string result;
            if (alAttachments.Count > 0)
                result = Emailer.SendMessageWithAttachment(txtSendTo.Text, txtSendFrom.Text, txtSubjectLine.Text, txtMessage.Text, alAttachments);
            else
                result = Emailer.SendMessage(txtSendTo.Text, txtSendFrom.Text, txtSubjectLine.Text, txtMessage.Text);
            MessageBox.Show(result, "Email Transmittal");*/

            MessageBox.Show("Email Sent");
             
        
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            //bottone Video Gallery carica i video nella listview1
            listView1.Items.Clear();
            listaFirstFrame.Images.Clear();
            String[] ImageFiles = Directory.GetFiles("./Video/");
            foreach (var file in ImageFiles)
            {
                //Add images to Imagelist
                if(file.EndsWith("bmp"))
                   listaFirstFrame.Images.Add(Image.FromFile(file));
            }
            listView1.View = View.LargeIcon;
            listaFirstFrame.ImageSize = new Size(160, 120);
            listView1.LargeImageList = listaFirstFrame;
            
            for (int j = 0; j < listaFirstFrame.Images.Count; j++)
            {
                ListViewItem item = new ListViewItem();
                item.ImageIndex = j;
                this.listView1.Items.Add(item);
            }


        }


        /*
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
        }*/
    }
}
