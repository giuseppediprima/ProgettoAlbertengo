using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;

using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;

using GHI.Premium.System;
using GHI.Premium.Net;

namespace ProgettoAlbertengo
{
    public partial class Program
    {
        //Stato d'avanzamento del programma
        static int stato = 0;
        Window mainWindow;
        StackPanel initPanel;
        StackPanel initPanel1;
        StackPanel initPanel2;
        StackPanel takePhoto;
        StackPanel videoStreaming;
        StackPanel gallery;
        StackPanel pc;

        Image photoImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.photoImg), Bitmap.BitmapImageType.Bmp));
        Image videoImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.videoImg), Bitmap.BitmapImageType.Bmp));
        Image galleryImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.galleryImg), Bitmap.BitmapImageType.Bmp));
        Image pcImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.pcImg), Bitmap.BitmapImageType.Bmp));
        Image photoImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.photoImgPressed), Bitmap.BitmapImageType.Bmp));
        Image videoImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.videoImgPressed), Bitmap.BitmapImageType.Bmp));
        Image galleryImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.galleryImgPressed), Bitmap.BitmapImageType.Bmp));
        Image pcImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.pcImgPressed), Bitmap.BitmapImageType.Bmp));
        Bitmap errorImgBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.error), Bitmap.BitmapImageType.Bmp);

        StackPanel savePhotoPanel;
        StackPanel labelSavePhoto;
        StackPanel buttonSavePanel;
        StackPanel confirmSave;
        StackPanel cancelSave;
        

        Image labelSavePhotoImg;
        Image confirmSaveImg;
        Image cancelSaveImg;
        Image confirmSaveImgPressed;
        Image cancelSaveImgPressed;


        StackPanel trashPanel;
        Image emptyTrash;
        Image fullTrash;

        Bitmap sdBmpImage = new Bitmap(Resources.GetBytes(Resources.BinaryResources.SD_card), Bitmap.BitmapImageType.Bmp);
        Bitmap noBmpImage = new Bitmap(Resources.GetBytes(Resources.BinaryResources.no_image), Bitmap.BitmapImageType.Bmp);
        Bitmap currentBitmapData;
        GT.Timer timer;

        Socket s = null;

        string[] immaginiGalleria;
        int numeroImmagini, indice;
        long start = 0, end = 0;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            SetupDisplay();
            SetupNet();

            wifi.NetworkDown += new GTM.Module.NetworkModule.NetworkEventHandler(wifi_NetworkDown);
            wifi.NetworkUp += new GTM.Module.NetworkModule.NetworkEventHandler(wifi_NetworkUp);
            
            ethernet.NetworkDown += new GTM.Module.NetworkModule.NetworkEventHandler(ethernet_NetworkDown);
            ethernet.NetworkUp += new GTM.Module.NetworkModule.NetworkEventHandler(ethernet_NetworkUp);
            sdCard.SDCardUnmounted += new SDCard.SDCardUnmountedEventHandler(sdCard_Unmounted);
            sdCard.SDCardMounted += new SDCard.SDCardMountedEventHandler(sdCard_Mounted);
            backButton.ButtonPressed += new Button.ButtonEventHandler(backButton_Pressed);
            confirmButton.ButtonPressed += new Button.ButtonEventHandler(confirmButton_Pressed);
            camera.BitmapStreamed += new Camera.BitmapStreamedEventHandler(camera_BitmapStreamed);
            timer = new GT.Timer(100);
            timer.Tick += timer_Tick;
            ledSd.TurnRed();

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Debug.Print("Program Started");
            
        }
        
        // // // // // // // START EVENT HANDLERS // // // // // // // // //
        private void ethernet_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Ethernet Network UP");
            ledNet.TurnGreen();
        }

        private void ethernet_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Ethernet Network Down");
            ledNet.TurnRed();
            if (stato == 4)
            {
                mainWindow.Background = new SolidColorBrush(Color.Black);
                stato = 0;
                ShowInitButtons();
                Thread t = new Thread(new ThreadStart(() => { if (s != null) s.Close(); }));
                t.Priority = ThreadPriority.BelowNormal;
                t.Start();
            }
        }

        private void wifi_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            /*
            Debug.Print("Network UP");
            ledNet.TurnBlue();

            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            String ipAddr = "127.0.0.1";
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostEntry(ipAddr).AddressList[0], 13000);
            s.Connect(remoteEP);
            s.Send(System.Text.UTF8Encoding.UTF8.GetBytes("ciao minchia"));
            s.Close();
            ledNet.TurnOff();
            */
        }

        private void wifi_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Network DOWN");
            //new Thread(new ThreadStart(connect)).Start();
            ledNet.TurnBlue();
        }

        private void sdCard_Mounted(SDCard sender, GT.StorageDevice SDCard)
        {
            ledSd.TurnGreen();
            if (stato == 3)
                mostraGalleria();
            if (stato == 1)
                camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
            if (stato == 5)
                startUpload();
        }

        private void sdCard_Unmounted(SDCard sender)
        {
            if (!sdCard.IsCardInserted || !sdCard.IsCardMounted){
                ledSd.TurnRed();

                if (stato == 1)
                {
                    camera.StopStreamingBitmaps();
                    mainWindow.Background = new ImageBrush(sdBmpImage);
                    display.SimpleGraphics.ClearNoRedraw();
                }

                if (sdCard.IsCardInserted)
                {
                    Thread t = new Thread(new ThreadStart(() =>
                    {
                        try { sdCard.MountSDCard(); }
                        catch (Exception e) { }
                    }));
                    t.Priority = ThreadPriority.BelowNormal;
                    t.Start();
                }
            }
        }

        private void camera_BitmapStreamed(Camera sender, Bitmap bitmap)
        {
            if (start == 0)
                start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            else if(end == 0)
            {
                end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Print("Streaming Bitmap time: " + (end - start));
                start = end;
                end = 0;
            }
            currentBitmapData = bitmap;
            if (stato == 1)
                display.SimpleGraphics.DisplayImage(bitmap, 0, 0);
            if (stato == 4)
            {
                Thread t = new Thread(new ThreadStart(sendImage));
                t.Priority = ThreadPriority.BelowNormal;
                t.Start();
            }
        }

        private void backButton_Pressed(Button sender, Button.ButtonState state)
        {
            Debug.Print("Back Button pressed. State: " + stato);
            switch (stato)
            {
                case 1:
                    // Take Photo state
                    camera.StopStreamingBitmaps();
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    ShowInitButtons();
                    stato = 0;
                    break;
                case 2:
                    // Save Image state
                    HideSavePhotoButtons();
                    camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
                    stato = 1;
                    break;

                case 3:
                    // Gallery state
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    timer.Stop();
                    mainWindow.Child = new StackPanel();
                    ShowInitButtons();
                    stato = 0;
                    break;

                case 4:
                    //VideoStreaming state
                    ledNet.TurnGreen();
                    camera.StopStreamingBitmaps();
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    stato = 0;
                    ShowInitButtons();
                    Thread t = new Thread(new ThreadStart(() => {
                        try
                        {
                            if (s != null) s.Close();
                        }
                        catch (Exception e)
                        {
                            Debug.Print(e.StackTrace);
                        }
                    }));
                    t.Priority = ThreadPriority.BelowNormal;
                    t.Start();
                    break;

                default:
                    ShowInitButtons();
                    break;
            }
        }

        private void confirmButton_Pressed(Button sender, Button.ButtonState state)
        {
            Debug.Print("Confirm Button pressed. State: " + stato);
            switch (stato)
            {
                case 1:
                    stato = 2;
                    camera.StopStreamingBitmaps();
                    ShowSavePhotoButtons();
                    break;
                case 2:
                    // Take Photo state
                    HideSavePhotoButtons();
                    Thread t = new Thread(savePicture);
                    t.Priority = ThreadPriority.BelowNormal;
                    t.Start();
                    ShowInitButtons();
                    stato = 0;
                    break;
            }
        }

        private void mainWindow_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            switch (stato)
            {
                case 1:
                    if (VerifySDCard())
                    {
                        stato = 2;
                        camera.StopStreamingBitmaps();
                        ShowSavePhotoButtons();
                    }
                    break;
            }
        }

        private void gallery_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Galley Button pressed...");
            gallery.Children.Clear();
            gallery.Children.Add(galleryImgPressed);
            mainWindow.Invalidate();
        }

        private void gallery_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Gallery Button released...");
            gallery.Children.Clear();
            gallery.Children.Add(galleryImg);
            mainWindow.Invalidate();

            HideInitButtons();
            timer.Start();
            stato = 3;
            mostraGalleria();
        }

        private void pc_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("PC Button pressed...");
            pc.Children.Clear();
            pc.Children.Add(pcImgPressed);
            mainWindow.Invalidate();
        }

        private void pc_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("PC Button released...");
            pc.Children.Clear();
            pc.Children.Add(pcImg);
            mainWindow.Invalidate();
            stato = 5;
            HideInitButtons();
                
            if (VerifySDCard())
            {
                startUpload();
            }
            else
                mainWindow.Background = new ImageBrush(sdBmpImage);
        }

        private void videoStreaming_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Video Button pressed...");
            videoStreaming.Children.Clear();
            videoStreaming.Children.Add(videoImgPressed);
            mainWindow.Invalidate();
        }

        private void videoStreaming_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Video Button released...");
            videoStreaming.Children.Clear();
            videoStreaming.Children.Add(videoImg);
            mainWindow.Invalidate();

            HideInitButtons();
            stato = 4;
            ledNet.BlinkRepeatedly(GT.Color.Orange);
            startVideoStreaming();
        }

        private void takePhoto_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Photo Button pressed...");
            takePhoto.Children.Clear();
            takePhoto.Children.Add(photoImgPressed);
            mainWindow.Invalidate();
        }

        private void takePhoto_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            Debug.Print("Photo Button released...");
            takePhoto.Children.Clear();
            takePhoto.Children.Add(photoImg);
            mainWindow.Invalidate();

            HideInitButtons();

            stato = 1;
            if (VerifySDCard())
                camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
            else
                mainWindow.Background = new ImageBrush(sdBmpImage);
        }

        private void cancelSave_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            cancelSave.Children.Clear();
            cancelSave.Children.Add(cancelSaveImg);
            mainWindow.Invalidate();

            HideSavePhotoButtons();

            stato = 1;
            camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
        }

        private void cancelSave_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            cancelSave.Children.Clear();
            cancelSave.Children.Add(cancelSaveImgPressed);
            mainWindow.Invalidate();
        }

        private void confirmSave_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            confirmSave.Children.Clear();
            confirmSave.Children.Add(confirmSaveImg);
            mainWindow.Invalidate();

            HideSavePhotoButtons();

            Thread t = new Thread(savePicture);
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
            stato = 0;
            ShowInitButtons();
        }

        private void confirmSave_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            confirmSave.Children.Clear();
            confirmSave.Children.Add(confirmSaveImgPressed);
            mainWindow.Invalidate();
        }

        private void trashPanel_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            trashPanel.Children.Clear();
            trashPanel.Children.Add(fullTrash);
            mainWindow.Invalidate();
        }

        private void trashPanel_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            trashPanel.Children.Clear();
            trashPanel.Children.Add(emptyTrash);
            mainWindow.Invalidate();
            removeImage();
            mostraGalleria();
        }

        private void timer_Tick(GT.Timer timer)
        {
            var pos = joystick.GetPosition();
            if (stato == 3)
            {
                if (VerifySDCard() && numeroImmagini != 0)
                {
                    if (pos.X > 0.9 || pos.Y > 0.9)
                    {
                        if (indice >= numeroImmagini - 1)
                            indice = -1;
                        mainWindow.Background = new ImageBrush(new GT.Picture(sdCard.GetStorageDevice().ReadFile("\\" + immaginiGalleria[++indice]), GT.Picture.PictureEncoding.BMP).MakeBitmap());
                    }
                    if (pos.X < -0.9 || pos.Y < -0.9)
                    {
                        if (indice == 0)
                            indice = (int)numeroImmagini;
                        mainWindow.Background = new ImageBrush(new GT.Picture(sdCard.GetStorageDevice().ReadFile("\\" + immaginiGalleria[--indice]), GT.Picture.PictureEncoding.BMP).MakeBitmap());
                    }
                    //Debug.Print("Showing image N°: " + showIndex);
                }
                else if (!VerifySDCard())
                {
                    mainWindow.Background = new ImageBrush(sdBmpImage);
                }
                else if (numeroImmagini == 0)
                {
                    mainWindow.Background = new ImageBrush(noBmpImage);
                }
            }
        }
        // // // // // // // END EVENT HANDLERS // // // // // // // // //


        private void SetupNet()
        {
            if (!wifi.Interface.IsOpen)
                wifi.Interface.Open();
            //wifi.UseDHCP();
            //wifi.UseStaticIP("169.254.50.93", "255.255.0.0", "169.254.50.91");
            //wifi.UseThisNetworkInterface();
            
            if (!ethernet.Interface.IsOpen)
                ethernet.Interface.Open();
            //ethernet.UseDHCP();
            ethernet.UseStaticIP("192.168.137.2", "255.255.255.0", "192.168.137.1");
            ethernet.UseThisNetworkInterface();

            Debug.Print("Net Setup finished");
            
        }

        private void connectStreamSocket()
        {
            Debug.Print("connectSocket started");
            try
            {
                Debug.Print("Create new Socket..");
                s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                String ipAddr = "192.168.137.1";

                Debug.Print("Generate EndPoint..");
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddr), 13000);
                Debug.Print("Connect..");
                s.Connect(remoteEP);
               
                Debug.Print("Connected!");
            }
            catch (Exception e) {
                Debug.Print(e.StackTrace);    
                s = null;
            }
        }

        private void connectUploadSocket()
        {
            Debug.Print("connectSocket started");
            Socket socket = null;
            try
            {
                Debug.Print("Create new Socket..");
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                String ipAddr = "192.168.137.1";
                Debug.Print("Generate EndPoint..");
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddr), 14000);
                Debug.Print("Connect..");
                socket.Connect(remoteEP);

                byte[] num_images = new byte[1];
                if (VerifySDCard())
                {
                    GT.StorageDevice storage = sdCard.GetStorageDevice();
                    string[] dirs = storage.ListDirectories("\\");
                    bool exist = false;
                    foreach (string d in dirs)
                    {
                        if (d.Equals("SD"))
                        {
                            exist = true;
                            break;
                        }
                    }

                    if (!exist)
                    {
                        num_images[0] = 0;
                        socket.Send(num_images);
                        return;
                    }

                    immaginiGalleria = storage.ListFiles("\\SD\\");

                    num_images[0] = (byte)immaginiGalleria.Length;
                    socket.Send(num_images);
                    if (num_images[0] == 0)
                    {
                        return;
                    }
                    else
                        Debug.Print("There are " + num_images[0] + " images");

                    for (int i = 0; i < num_images[0]; i++)
                    {
                        if (socket == null)
                            return;
                        Debug.Print("Send image " + i + "...");
                        Bitmap bmp = new GT.Picture(storage.ReadFile("\\" + immaginiGalleria[indice]), GT.Picture.PictureEncoding.BMP).MakeBitmap();
                        Byte[] outputFileBuffer = new Byte[bmp.Height * bmp.Width * 3 + 54];
                        Util.BitmapToBMPFile(bmp.GetBitmap(), bmp.Width, bmp.Height, outputFileBuffer);
                        socket.Send(outputFileBuffer);
                        Thread.Sleep(500);
                    }

                    byte[] msg = new byte[1];
                    socket.Receive(msg);

                    if (msg[0] == 0)
                    {
                        storage.DeleteDirectory("\\SD\\", true);
                        storage.CreateDirectory("\\SD\\");
                    }
                }
                else
                {
                    num_images[0] = 0;
                    socket.Send(num_images);
                }
            }
            catch (Exception e) 
            {
                Debug.Print("Exception: " + e.StackTrace);
            }
            finally
            {
                if (socket != null)
                    socket.Close();
            }
        }

        private void sendImage()
        {
            try
            {
                if (s == null) 
                    return;
                Debug.Print("Send image...");
                Byte[] outputFileBuffer = new Byte[currentBitmapData.Height * currentBitmapData.Width * 3 + 54];
                Util.BitmapToBMPFile(currentBitmapData.GetBitmap(), currentBitmapData.Width, currentBitmapData.Height, outputFileBuffer);
                s.Send(outputFileBuffer);
            }
            catch (Exception e) 
            { }
        }

        private void connectEthernet()
        {
            if (!ethernet.IsNetworkUp || !ethernet.IsNetworkConnected)
            {
                Debug.Print("Network Up: " + ethernet.IsNetworkUp + "\n" + "Network Connected: " + ethernet.IsNetworkConnected);
                return;
            }
        }

        private void connectWifi()
        {
            /*GHI.Premium.Net.WiFiNetworkInfo info = new WiFiNetworkInfo();
            info.SSID = "Fezspider";
            info.SecMode = SecurityMode.Open;
            info.networkType = NetworkType.AdHoc;
            
            while (!wifi.IsNetworkConnected)
            {
                wifi.Interface.Join(info, "");
                if (!wifi.IsNetworkConnected)
                {
                    Debug.Print("Non Connesso....");
                    Thread.Sleep(2000);
                }
            }

            Debug.Print("Inizio a sistemare il Socket");
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            String ipAddr = "169.254.50.91";
            Debug.Print("Preparo il RemoteEP");
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddr), 13000);
            Debug.Print("Connecting to "+ipAddr+" ...");
            s.Connect(remoteEP);
            Debug.Print("Sending...");
            s.Send(System.Text.UTF8Encoding.UTF8.GetBytes("ciao minchia"));
            Debug.Print("Closing...");
            s.Close();
            Debug.Print("Led OFF");
            ledNet.TurnOff();
             */
        }

        private void SetupDisplay()
        {
            mainWindow = display.WPFWindow;
            mainWindow.Background = new SolidColorBrush(Color.Black);
            SetupInitWindow();
            SetupTrashPanel();
            SetupSavePhotoWindow();

            ShowInitButtons();

            mainWindow.TouchDown += mainWindow_TouchDown;
            Debug.Print("Display Setup finished");
        }

        private void SetupTrashPanel()
        {
            trashPanel = new StackPanel(Orientation.Horizontal);
            Bitmap fullTrashBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.full_trash), Bitmap.BitmapImageType.Bmp);
            Bitmap emptyTrashBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.empty_trash), Bitmap.BitmapImageType.Bmp);

            fullTrashBmp.MakeTransparent(fullTrashBmp.GetPixel(1, 1));
            emptyTrashBmp.MakeTransparent(emptyTrashBmp.GetPixel(1, 1));
            fullTrash = new Image(fullTrashBmp);
            emptyTrash = new Image(emptyTrashBmp);
            trashPanel.Children.Add(emptyTrash);
            trashPanel.HorizontalAlignment = HorizontalAlignment.Right;
            trashPanel.VerticalAlignment = VerticalAlignment.Bottom;
            trashPanel.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(trashPanel_TouchDown);
            trashPanel.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(trashPanel_TouchUp);
        }

        private void SetupSavePhotoWindow()
        {
            MakeTrasparentImg();

            savePhotoPanel = new StackPanel(Orientation.Vertical);
            labelSavePhoto = new StackPanel(Orientation.Horizontal);
            buttonSavePanel = new StackPanel(Orientation.Horizontal);
            confirmSave = new StackPanel(Orientation.Horizontal);
            cancelSave = new StackPanel(Orientation.Horizontal);

            savePhotoPanel.SetMargin(6);
            labelSavePhoto.SetMargin(6);
            buttonSavePanel.SetMargin(6);
            confirmSave.SetMargin(6);
            cancelSave.SetMargin(6);

            labelSavePhoto.HorizontalAlignment = HorizontalAlignment.Center;
            buttonSavePanel.HorizontalAlignment = HorizontalAlignment.Center;
            confirmSave.HorizontalAlignment = HorizontalAlignment.Center;
            cancelSave.HorizontalAlignment = HorizontalAlignment.Center;

            labelSavePhoto.Children.Add(labelSavePhotoImg);
            confirmSave.Children.Add(confirmSaveImg);
            cancelSave.Children.Add(cancelSaveImg);

            buttonSavePanel.Children.Add(confirmSave);
            buttonSavePanel.Children.Add(cancelSave);
            savePhotoPanel.Children.Add(labelSavePhoto);
            savePhotoPanel.Children.Add(buttonSavePanel);

            confirmSave.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(confirmSave_TouchDown);
            cancelSave.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(cancelSave_TouchDown);

            confirmSave.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(confirmSave_TouchUp);
            cancelSave.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(cancelSave_TouchUp);
        }

        private void MakeTrasparentImg()
        {
            Bitmap labelSavePhotoBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.labelSavePhotoImg), Bitmap.BitmapImageType.Bmp);
            Bitmap confirmSaveBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.confirmSaveImg), Bitmap.BitmapImageType.Bmp);
            Bitmap cancelSaveBmp = new Bitmap(Resources.GetBytes(Resources.BinaryResources.cancelSaveImg), Bitmap.BitmapImageType.Bmp);
            Bitmap confirmSaveBmpPressed = new Bitmap(Resources.GetBytes(Resources.BinaryResources.confirmSaveImgPressed), Bitmap.BitmapImageType.Bmp);
            Bitmap cancelSaveBmpPressed = new Bitmap(Resources.GetBytes(Resources.BinaryResources.cancelSaveImgPressed), Bitmap.BitmapImageType.Bmp);

            labelSavePhotoBmp.MakeTransparent(labelSavePhotoBmp.GetPixel(1, 1));
            confirmSaveBmp.MakeTransparent(confirmSaveBmp.GetPixel(1, 1));
            cancelSaveBmp.MakeTransparent(cancelSaveBmp.GetPixel(1, 1));
            confirmSaveBmpPressed.MakeTransparent(confirmSaveBmpPressed.GetPixel(1, 1));
            cancelSaveBmpPressed.MakeTransparent(cancelSaveBmpPressed.GetPixel(1, 1));

            labelSavePhotoImg = new Image(labelSavePhotoBmp);
            confirmSaveImg = new Image(confirmSaveBmp);
            cancelSaveImg = new Image(cancelSaveBmp);
            confirmSaveImgPressed = new Image(confirmSaveBmpPressed);
            cancelSaveImgPressed = new Image(cancelSaveBmpPressed);
        }

        private void SetupInitWindow()
        {
            //Create StackPanels
            initPanel = new StackPanel(Orientation.Vertical);
            initPanel1 = new StackPanel(Orientation.Horizontal);
            initPanel2 = new StackPanel(Orientation.Horizontal);
            takePhoto = new StackPanel(Orientation.Horizontal);
            videoStreaming = new StackPanel(Orientation.Horizontal);
            gallery = new StackPanel(Orientation.Horizontal);
            pc = new StackPanel(Orientation.Horizontal);
            StackPanel separator = new StackPanel(Orientation.Horizontal);
            StackPanel separator1 = new StackPanel(Orientation.Horizontal);

            //Set Margin to 6 for each StackPanel
            initPanel1.SetMargin(2);
            initPanel2.SetMargin(2);
            takePhoto.SetMargin(6);
            videoStreaming.SetMargin(6);
            gallery.SetMargin(6);
            pc.SetMargin(6);
            separator.Height = separator.Width = 20;
            separator1.Height = separator1.Width = 20;
            //Center align StackPanel Horizontally
            initPanel1.HorizontalAlignment = HorizontalAlignment.Center;
            initPanel2.HorizontalAlignment = HorizontalAlignment.Center;
            takePhoto.HorizontalAlignment = HorizontalAlignment.Center;
            videoStreaming.HorizontalAlignment = HorizontalAlignment.Center;
            gallery.HorizontalAlignment = HorizontalAlignment.Center;
            pc.HorizontalAlignment = HorizontalAlignment.Center;
            //Create Associations between StackPanels and Images
            takePhoto.Children.Add(photoImg);
            videoStreaming.Children.Add(videoImg);
            gallery.Children.Add(galleryImg);
            pc.Children.Add(pcImg);
            initPanel1.Children.Add(takePhoto);
            initPanel1.Children.Add(separator);
            initPanel1.Children.Add(gallery);
            initPanel2.Children.Add(videoStreaming);
            initPanel2.Children.Add(separator1);
            initPanel2.Children.Add(pc);
            initPanel.Children.Add(initPanel1);
            initPanel.Children.Add(initPanel2);

            takePhoto.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(takePhoto_TouchDown);
            videoStreaming.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(videoStreaming_TouchDown);
            gallery.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(gallery_TouchDown);
            pc.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(pc_TouchDown);

            takePhoto.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(takePhoto_TouchUp);
            videoStreaming.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(videoStreaming_TouchUp);
            gallery.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(gallery_TouchUp);
            pc.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(pc_TouchUp);
        }

        private void HideInitButtons()
        {
            mainWindow.Child = new StackPanel();
            mainWindow.Invalidate();
        }

        private void ShowInitButtons()
        {
            mainWindow.Child = initPanel;
            mainWindow.Invalidate();
        }

        private void HideSavePhotoButtons()
        {
            mainWindow.Child = new StackPanel();
            mainWindow.Background = new SolidColorBrush(Color.Black);
            mainWindow.Invalidate();
        }

        private void ShowSavePhotoButtons()
        {
            mainWindow.Child = savePhotoPanel;
            mainWindow.Background = new ImageBrush(currentBitmapData);
            mainWindow.Invalidate();
        }

        private void startVideoStreaming()
        {
            Bitmap tmp = new Bitmap(320, 240);
            tmp.DrawText("Press Back Button to end", Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Red, 50, 100);
            mainWindow.Background = new ImageBrush(tmp);
            
            Thread t = new Thread(new ThreadStart(connectStreamSocket));
            t.Priority = ThreadPriority.Normal;
            t.Start();
            while (t.IsAlive)
                Thread.Sleep(200);

            camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
            
        }

        private void startUpload()
        {
            mainWindow.Background = new SolidColorBrush(Color.Black);
            display.SimpleGraphics.ClearNoRedraw();
            display.SimpleGraphics.DisplayText("Upload images... Please Wait...", Resources.GetFont(Resources.FontResources.NinaB), GT.Color.White, 50, 100);
            ledNet.BlinkRepeatedly(GT.Color.Orange);
            
            Thread t = new Thread(new ThreadStart(connectUploadSocket));
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();

            uint i = 0;
            while (t.IsAlive)
            {
                //TODO sostituire con qualcos'altro perchè blocca la grafica
                if (i > 200)
                {
                    display.SimpleGraphics.ClearNoRedraw();
                    display.SimpleGraphics.DisplayText("Upload images... Please Wait...", Resources.GetFont(Resources.FontResources.NinaB), GT.Color.White, 50, 100);
                    i -= 200;
                }
                display.SimpleGraphics.DisplayEllipse(GT.Color.White, 60 + i, 150, 5, 5);
                i += 15;
                
                Thread.Sleep(100);
            }

            ledNet.TurnGreen();
            mainWindow.Background = new SolidColorBrush(Color.Black);
            stato = 0;
            ShowInitButtons();
        }

        private void removeImage()
        {
            try
            {
                ledSd.BlinkRepeatedly(GT.Color.Orange);
                sdCard.GetStorageDevice().Delete("\\SD\\Image-" + indice + ".jpg");
                if (numeroImmagini != 1 && indice != (numeroImmagini - 1))
                {
                    sdCard.GetStorageDevice().WriteFile("\\SD\\Image-" + indice + ".jpg", sdCard.GetStorageDevice().ReadFile("\\SD\\Image-" + (numeroImmagini - 1) + ".jpg"));
                    sdCard.GetStorageDevice().Delete("\\SD\\Image-" + (numeroImmagini - 1) + ".jpg");
                }
                else
                {
                    mainWindow.Child = new StackPanel();
                }
                sdCard.UnmountSDCard();
            }
            catch (Exception e)
            {
                mainWindow.Background = new ImageBrush(errorImgBmp);
                Debug.Print(e.StackTrace);
            }
        }

        private void savePicture()
        {
            if (VerifySDCard())
            {
                try
                {
                    ledSd.BlinkRepeatedly(GT.Color.Blue);
                    GT.StorageDevice storage = sdCard.GetStorageDevice();
                    string[] dirs = storage.ListDirectories("\\");
                    bool exist = false;
                    foreach (string d in dirs)
                    {
                        if (d.Equals("SD"))
                        {
                            exist = true;
                            break;
                        }
                    }
                    if (!exist)
                        storage.CreateDirectory("\\SD\\");
                    uint imageNumber = (uint)storage.ListFiles("\\SD\\").Length;

                    byte[] bytes = new byte[currentBitmapData.Width * currentBitmapData.Height * 3 + 54];
                    Util.BitmapToBMPFile(currentBitmapData.GetBitmap(), currentBitmapData.Width, currentBitmapData.Height, bytes);
                    GT.Picture picture = new GT.Picture(bytes, GT.Picture.PictureEncoding.JPEG);

                    string pathFileName = "\\SD\\Image-" + (imageNumber++).ToString() + ".jpg";

                    storage.WriteFile(pathFileName, picture.PictureData);
                    sdCard.UnmountSDCard();    
                }
                catch (Exception ex)
                {
                    mainWindow.Background = new ImageBrush(errorImgBmp);
                    Debug.Print("Message: " + ex.Message + "  Inner Exception: " + ex.InnerException);
                }
                finally
                {
                    ledSd.TurnOff();
                }
            }
            
        }
        
        private bool VerifySDCard()
        {
            if (sdCard.IsCardInserted)
            {
                if (!sdCard.IsCardMounted)
                {
                    try
                    {
                        sdCard.MountSDCard();
                        if (sdCard.IsCardMounted)
                            return true;
                        else
                            return false;
                    }
                    catch (Exception e)
                    {
                        return false;
                    }
                }
                else
                    return true;
            }
            return false;
        }

        private void mostraGalleria()
        {
            //mostra la galleria delle immagini salvate
            if (VerifySDCard())
            {
                try
                {
                    GT.StorageDevice storage = sdCard.GetStorageDevice();
                    string[] dirs = storage.ListDirectories("\\");
                    bool exist = false;
                    foreach (string d in dirs)
                    {
                        if (d.Equals("SD"))
                        {
                            exist = true;
                            break;
                        }
                    }

                    if (!exist)
                    {
                        mainWindow.Background = new ImageBrush(noBmpImage);
                        return;
                    }

                    immaginiGalleria = storage.ListFiles("\\SD\\");

                    if ((numeroImmagini = (int)immaginiGalleria.Length) == 0)
                    {
                        mainWindow.Background = new ImageBrush(noBmpImage);
                        Debug.Print("Nessun File in SD...");

                        return;
                    }

                    indice = 0;
                    mainWindow.Background = new ImageBrush(new GT.Picture(storage.ReadFile("\\" + immaginiGalleria[indice]), GT.Picture.PictureEncoding.BMP).MakeBitmap());
                    mainWindow.Child = trashPanel;
                }
                catch (Exception e)
                {
                    mainWindow.Background = new ImageBrush(errorImgBmp);
                    Debug.Print(e.StackTrace);
                }
            }
            else
            {
                mainWindow.Background = new ImageBrush(sdBmpImage);
                Debug.Print("Nessuna SD...");
            }
        }
    }
}
