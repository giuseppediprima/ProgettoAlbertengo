using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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
        int attachedCounter = 0;
        long totalAttachmentSize = 0;
        bool sizeExceeded = false;
        ArrayList attachments = new ArrayList();
        bool areImages = true;

        public Form1()
        {
            InitializeComponent();
            
            pictureBox1.Image = pictureBox1.InitialImage;
            listaFirstFrame = new ImageList();

            button5_Click(null, null);
            
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
            byte[] bytes = new byte[230454];
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
                        images.Add((Image)data.Clone());
                        if (++images_read == num_images[0])
                            break;
                        bytes = new byte[230454];
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
                        command.CommandText = "Insert into Images (image) VALUES (@image)";
                        command.Parameters.Add("@image", SqlDbType.VarBinary);
                        for (int i = images_read - 1; i >= 0; i--)
                        {
                            byte[] img = imageToByteArray(images[i]);
                            command.Parameters["@image"].Value = img;
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
            finally
            {
                this.Invoke((MethodInvoker)delegate()
                {
                    if (areImages)
                        button5_Click(null, null);
                });
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
                this.Invoke((MethodInvoker)delegate()
                {
                    if (!areImages)
                        button1_Click(null, null);
                });
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

        public static Image byteArrayToImage(byte[] byteArrayIn)
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
            if (sender.GetType() != typeof(bool) || (bool)sender)
            {
                txtSendTo.Text = "";
                txtMessage.Text = "";
                txtSubjectLine.Text = "";
            }

            foreach (Control c in flowLayoutPanel1.Controls)
                foreach (Control x in c.Controls)
                    if (x.GetType() == typeof(CheckBox))
                        ((CheckBox)x).Checked = false;
            label1.Text = "No Selected Attachments";
            attachedCounter = 0;
            totalAttachmentSize = 0;
            attachments.Clear();
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

            hideSendButton();

            new Thread(new ThreadStart(() =>
            {
                try
                {
                    if (attachments.Count != 0)
                    {
                        string zipFile = createZipFile();
                        if (zipFile == null)
                        {
                            MessageBox.Show("Error creating attachments zip file");
                            showSendButton();
                            return;
                        }
                        attachments.Clear();
                        attachments.Add(zipFile);
                    }

                    string result;
                    if (attachments.Count > 0)
                    {
                        result = Emailer.SendMessageWithAttachment(txtSendTo.Text, txtSendFrom.Text, txtSubjectLine.Text, txtMessage.Text, attachments);
                        File.Delete((string)attachments[0]);
                    }
                    else
                        result = Emailer.SendMessage(txtSendTo.Text, txtSendFrom.Text, txtSubjectLine.Text, txtMessage.Text);
                    this.Invoke((MethodInvoker)delegate() { MessageBox.Show(result, "Email Transmittal"); });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: {0}\n{1}", ex.Message, ex.StackTrace);
                }
                finally
                {
                    this.Invoke((MethodInvoker)delegate()
                    {
                        showSendButton();
                    });
                }
            })).Start();
        }

        private void showSendButton()
        {
            pb.Hide();
            btnSend.Show();
        }

        private void hideSendButton()
        {
            btnSend.Hide();
            pb.Show();
        }

        private string createZipFile()
        {
            string zipPath = @"./attachments.zip";
            try
            {
                ImagesTableAdapter ita = new ImagesTableAdapter();
                if (!Directory.Exists("./Zip/"))
                {
                    Directory.CreateDirectory("./Zip/");
                }
                foreach(String s in attachments)
                    if (s.StartsWith("Image:"))
                    {
                        Server.MyImageDataSet.ImagesDataTable idt = ita.GetImageById(long.Parse(s.Replace("Image: ", "")));
                        new Bitmap(byteArrayToImage((byte[])idt.Rows[0].ItemArray[1])).Save("./Zip/" + s.Replace(":", "_") + ".bmp", ImageFormat.Bmp);
                    }
                    else
                    {
                        File.Copy(s, s.Replace("./Video", "./Zip"));
                    }
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                ZipFile.CreateFromDirectory("./Zip", zipPath);
                Directory.Delete("./Zip", true);
                return zipPath;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            areImages = false;
            flowLayoutPanel1.Hide();
            //bottone Video Gallery carica i video nella listview1
            flowLayoutPanel1.Controls.Clear();
            
            String[] ImageFiles = Directory.GetFiles("./Video/");
            foreach (var file in ImageFiles)
            {
                //Add images to Imagelist
                if (file.EndsWith("bmp"))
                {
                    PanelItem panel = new PanelItem(file.Replace("./Video/", "Video: "), Image.FromFile(file));
                    flowLayoutPanel1.Controls.Add(panel);
                    if (attachments.Contains(file.Replace("bmp", "avi")))
                        panel.checkBox1.Checked = true;
                    panel.checkBox1.CheckedChanged += checkBox1_CheckedChanged;
                    panel.button2.Click += button2_Click;
                }
            }
            if (flowLayoutPanel1.Controls.Count == 0)
                flowLayoutPanel1.BackgroundImage = (Image)(resources.GetObject("flowLayoutPanel1.BackgroundImage"));
            else
                flowLayoutPanel1.BackgroundImage = null;
            flowLayoutPanel1.Show();
        }

        void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox check = (CheckBox)sender;
            string fileName;
            long fileSize;
            if (!areImages)
            {
                fileName = ((string)check.Tag).Replace("Video: ", "./Video/").Replace("bmp", "avi");
                fileSize = getItemSize(fileName);
            }
            else
            {
                fileName = (string)check.Tag;
                fileSize = 230454;
            }
            if (check.Checked)
            {
                if ((fileSize + totalAttachmentSize) >= 24 * 1000 * 1000)
                {
                    MessageBox.Show("Attacment Size exceeded");
                    sizeExceeded = true;
                    check.Checked = false;
                    return;
                }
                attachedCounter++;
                totalAttachmentSize += fileSize;
                attachments.Add(fileName);

                label1.Text = "Attachments: " + attachedCounter + " - Total Size: " + getConvertedTotalAttachmentSize();
            }
            else if (!sizeExceeded)
            {
                totalAttachmentSize -= fileSize;
                if (--attachedCounter == 0)
                    label1.Text = "No Selected Attachments";
                else
                    label1.Text = "Attachments: " + attachedCounter + " - Total Size: " + getConvertedTotalAttachmentSize();
                attachments.Remove(fileName);
            }
            else
                sizeExceeded = false;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            areImages = true;
            flowLayoutPanel1.Hide();
            flowLayoutPanel1.Controls.Clear();
            
            ImagesTableAdapter ita = new ImagesTableAdapter();
            Server.MyImageDataSet.ImagesDataTable idt = ita.GetData();
            if (idt.Rows.Count == 0)
                flowLayoutPanel1.BackgroundImage = (Image)(resources.GetObject("flowLayoutPanel1.BackgroundImage"));
            else
                flowLayoutPanel1.BackgroundImage = null;

            foreach (System.Data.DataRow image in idt.Rows)
            {
                string tag = "Image: " + image.ItemArray[0];
                PanelItem panel = new PanelItem(tag, byteArrayToImage((byte[])image.ItemArray[1]));
                flowLayoutPanel1.Controls.Add(panel);
                if (attachments.Contains(tag))
                    panel.checkBox1.Checked = true;
                panel.checkBox1.CheckedChanged += checkBox1_CheckedChanged;
                panel.button2.Click += button2_Click;
            }
            flowLayoutPanel1.Show();
        }

        void button2_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            foreach (Control c in btn.Parent.Controls)
                if (c.GetType() == typeof(CheckBox))
                    if (((CheckBox)c).Checked)
                        ((CheckBox)c).Checked = false;
            if (areImages)
            {
                ImagesTableAdapter ita = new ImagesTableAdapter();
                try
                {
                    int result = ita.Delete(long.Parse(((string)btn.Tag).Replace("Image: ", "")));
                    if (result == 0)
                        throw new Exception("No rows affected");
                    flowLayoutPanel1.Controls.Remove(btn.Parent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Sorry!!!\nError occurs deleting File.\n" + ex.Message, "Error...");
                }
            }
            else
            {
                string path = ((string)btn.Tag).Replace("Video: ", "./Video/");
                try
                {
                    btn.Parent.BackgroundImage.Dispose();
                    File.Delete(path);
                    File.Delete(path.Replace("bmp", "avi"));
                    flowLayoutPanel1.Controls.Remove(btn.Parent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Sorry!!!\nError occurs deleting File.\n" + ex.Message, "Error...");
                }
            }
            if (flowLayoutPanel1.Controls.Count == 0)
                flowLayoutPanel1.BackgroundImage = (Image)(resources.GetObject("flowLayoutPanel1.BackgroundImage"));
        }

        private string getConvertedTotalAttachmentSize()
        {
            if (totalAttachmentSize > 1024)
                if (totalAttachmentSize > 1024 * 1024)
                    return Math.Round(((float)totalAttachmentSize / (1024f * 1024f)), 2) + "MB";
                else
                    return Math.Round(((float)totalAttachmentSize / 1024f), 2) + "KB";
            else
                return totalAttachmentSize + "Bytes";
        }

        private long getItemSize(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            return info.Length;
        }
    }
}
