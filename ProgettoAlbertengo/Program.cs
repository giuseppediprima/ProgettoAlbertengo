﻿using System;
using System.Threading;
using System.Collections;
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

using Pacman;
using dotnetwarrior.NetMF.Diagnostics;
using dotnetwarrior.NetMF.Game;
using System.Reflection;

namespace ProgettoAlbertengo
{
    /// <summary>
    /// Main class of software on the Fez Spider.
    /// The program has been designed as a finite state machine, each screen on the display corresponds to a different state.
    /// Each tap on the display has a different function depending on the screen in which the user is located. 
    /// </summary>
    public partial class Program
    {
        #region GLOBAL variables
        private PacmanGame _pacmanGame = null;
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
        Bitmap skull = new Bitmap(Resources.GetBytes(Resources.BinaryResources.teschio), Bitmap.BitmapImageType.Bmp);
        Bitmap currentBitmapData;
        GT.Timer timer;

        Queue toSend = new Queue();
        static AutoResetEvent evento = new AutoResetEvent(true);
        Thread sending = null;

        string[] immaginiGalleria;
        int numeroImmagini, indice;
        long start = 0, end = 0;
        #endregion
       
        /// <summary>
        /// This method is run when the mainboard is powered up or reset. It initialize the display with the main window and create the events on the SD card and the
        /// ethernet network.
        /// </summary>
        void ProgramStarted()
        {
            SetupDisplay();
            SetupNet();

            ethernet.NetworkDown += new GTM.Module.NetworkModule.NetworkEventHandler(ethernet_NetworkDown);
            ethernet.NetworkUp += new GTM.Module.NetworkModule.NetworkEventHandler(ethernet_NetworkUp);
            sdCard.SDCardUnmounted += new SDCard.SDCardUnmountedEventHandler(sdCard_Unmounted);
            sdCard.SDCardMounted += new SDCard.SDCardMountedEventHandler(sdCard_Mounted);
            backButton.ButtonPressed += new Button.ButtonEventHandler(backButton_Pressed);
            //confirmButton.ButtonPressed += new Button.ButtonEventHandler(confirmButton_Pressed);
            camera.BitmapStreamed += new Camera.BitmapStreamedEventHandler(camera_BitmapStreamed);
            joystick.JoystickPressed += new Joystick.JoystickEventHandler(joystick_JoystickPressed);
            timer = new GT.Timer(100);
            timer.Tick += timer_Tick;
            ledSd.TurnRed();

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Debug.Print("Program Started");
        }

        #region EVENT HANDLERS
        private void joystick_JoystickPressed(Joystick sender, Joystick.JoystickState state)
        {
            if (stato == 0)
            {
                mainWindow.Child = new StackPanel();
                Bitmap back = new Bitmap(320, 240);
                back.DrawImage(0, 0, skull, 0, 0, 320, 240);

                Font font = Resources.GetFont(Resources.FontResources.counting);
                for (int j = 0; j <= 160; j++)
                {
                    Color color = chooseColor(j);
                    back.DrawEllipse(color, 1, 160, 120, j, j, color, 0, 0, color, 0, 0, 0);
                    display.SimpleGraphics.DisplayImage(back, 0, 0);
                    display.SimpleGraphics.DisplayText("" + (9-j/18), font, GT.Color.Black, (uint)(160-font.MaxWidth/2), (uint)(119-font.Height/2)); 
                    Thread.Sleep(100);
                }

                stato = 6;
                var surface = (Bitmap)(display.SimpleGraphics.GetType().GetField("_display", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(display.SimpleGraphics));

                _pacmanGame = new PacmanGame(surface);
                _pacmanGame.InputManager.AddInputProvider(new GhiJoystickInputProvider(joystick));
                _pacmanGame.Initialize();
            }
        }

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
            }
        }

        private void sdCard_Mounted(SDCard sender, GT.StorageDevice SDCard)
        {
            ledSd.TurnGreen();
            if( stato == 0)
                ShowInitButtons();
            if (stato == 1 || stato == 2)
            {
                stato = 1;
                camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
                confirmButton.ButtonPressed += new Button.ButtonEventHandler(confirmButton_Pressed);
            }
            if (stato == 3)
                mostraGalleria();
            if (stato == 5)
                startUpload();
        }

        private void sdCard_Unmounted(SDCard sender)
        {
            if (!sdCard.IsCardInserted || !sdCard.IsCardMounted)
            {
                ledSd.TurnRed();

                if (stato == 1 || stato == 2)
                {
                    stato = 1;
                    camera.StopStreamingBitmaps();
                    confirmButton.ButtonPressed -= new Button.ButtonEventHandler(confirmButton_Pressed);
                    mainWindow.Child = new StackPanel();
                    mainWindow.Background = new ImageBrush(sdBmpImage);
                    display.SimpleGraphics.ClearNoRedraw();
                }
                if (stato == 3)
                    mainWindow.Child = new StackPanel();

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
            else if (end == 0)
            {
                end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Debug.Print("Streaming Bitmap time: " + (end - start));
                start = end;
                end = 0;
            }
            if (stato == 1)
            {
                currentBitmapData = bitmap;
                display.SimpleGraphics.DisplayImage(bitmap, 0, 0);
            }
            if (stato == 4)
                if (ethernet.IsNetworkUp && ethernet.IsNetworkConnected && sending != null && sending.IsAlive )
                {
                    lock (toSend)
                    {
                        toSend.Enqueue(new MyBitmap(false, bitmap));
                        evento.Set();
                    }
                }
                else
                    backButton_Pressed(null, Button.ButtonState.Pressed);
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
                    if (ethernet.IsNetworkUp && ethernet.IsNetworkConnected)
                        ledNet.TurnGreen();
                    else
                        ledNet.TurnRed();
                    camera.StopStreamingBitmaps();
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    stato = 0;
                    ShowInitButtons();
                    lock (toSend)
                    {
                        toSend.Enqueue(new MyBitmap(true, null));
                        evento.Set();
                    }
                    break;
                case 6:
                    //this.Reboot();
                    stato = 0;
                    new Thread(new ThreadStart(() => { _pacmanGame._gameTimer.Dispose(); })).Start();
                    Thread.Sleep(500);
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    ShowInitButtons();
                    break;
                default:
                    mainWindow.Background = new SolidColorBrush(Color.Black);
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

            bool sdStatus = true;
            if ((sdStatus = VerifySDCard()) == true && ethernet.IsNetworkUp && ethernet.IsNetworkConnected)
            {
                stato = 5;
                HideInitButtons();
                startUpload();
            }
            else if (!sdStatus)
            {
                HideInitButtons();
                mainWindow.Background = new ImageBrush(sdBmpImage);
            }
            else
            {
                SetupNet();
                ledNet.TurnRed();
            }
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

            if (ethernet.IsNetworkUp && ethernet.IsNetworkConnected && (sending == null || !sending.IsAlive))
            {
                HideInitButtons();
                stato = 4;
                ledNet.BlinkRepeatedly(GT.Color.Orange);
                startVideoStreaming();
            }
            else if(!ethernet.IsNetworkUp || !ethernet.IsNetworkConnected)
            {
                SetupNet();
                ledNet.TurnRed();
            }
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
                    mainWindow.Child = new StackPanel();
                }
                else if (numeroImmagini == 0)
                {
                    mainWindow.Background = new ImageBrush(noBmpImage);
                    mainWindow.Child = new StackPanel();
                }
            }
        }
        #endregion

        #region NETWORK functions  
        /// <summary>
        /// Initializes the ethernet network with a static IP configuration.
        /// </summary>
        public void SetupNet()
        {
            if (!ethernet.Interface.IsOpen)
                ethernet.Interface.Open();
            ethernet.UseStaticIP("192.168.137.2", "255.255.255.0", "192.168.137.1");
            ethernet.UseThisNetworkInterface();

            Debug.Print("Net Setup finished");

        }
        /// <summary>
        /// Connects the mainboard with the server to trasmit the stream of the camera. 
        /// </summary>
        public void connectStreamSocket()
        {
            Socket socket = null;
            Debug.Print("connectSocket started");
            try
            {
                lock (toSend)
                {
                    toSend.Clear();
                }
                Debug.Print("Create new Socket..");
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                String ipAddr = "192.168.137.1";

                Debug.Print("Generate EndPoint..");
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddr), 13000);
                Debug.Print("Connect..");
                socket.Connect(remoteEP);

                socket.SendTimeout = 10000;
                camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
                while (true)
                {
                    bool noBitmap = true;
                    while (noBitmap)
                    {
                        lock (toSend)
                        {
                            if (toSend.Count != 0)
                                noBitmap = false;
                        }
                        if (noBitmap)
                            evento.WaitOne();
                    }
                    MyBitmap myBmp;
                    lock (toSend)
                    {
                        Debug.Print("Send image...");
                        myBmp = ((MyBitmap)toSend.Dequeue());
                    }
                    if (!myBmp.last)
                    {
                        currentBitmapData = myBmp.bitmap;
                        Byte[] outputFileBuffer = new Byte[currentBitmapData.Height * currentBitmapData.Width * 3 + 54];
                        Util.BitmapToBMPFile(currentBitmapData.GetBitmap(), currentBitmapData.Width, currentBitmapData.Height, outputFileBuffer);
                        socket.Send(outputFileBuffer);
                    }
                    else
                        break;

                }

                Debug.Print("Connected!");
            }
            catch (Exception e)
            {
                Debug.Print(e.StackTrace);
            }
            finally
            {
                if (socket != null)
                    socket.Close();
            }
        }
        /// <summary>
        /// Connects the mainboard with the server to move the picture in the SD card to the database's server. 
        /// </summary>
        public void connectUploadSocket()
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

                socket.SendTimeout = 10000;
                byte[] num_images = new byte[1];
                if (VerifySDCard())
                {
                    GT.StorageDevice storage = sdCard.GetStorageDevice();
                    if (storage == null)
                        throw new Exception();
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
                        Bitmap bmp = new GT.Picture(storage.ReadFile("\\" + immaginiGalleria[i]), GT.Picture.PictureEncoding.BMP).MakeBitmap();
                        Byte[] outputFileBuffer = new Byte[bmp.Height * bmp.Width * 3 + 54];
                        Util.BitmapToBMPFile(bmp.GetBitmap(), bmp.Width, bmp.Height, outputFileBuffer);
                        socket.Send(outputFileBuffer);
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
                if (ethernet.IsNetworkUp && ethernet.IsNetworkConnected)
                    ledNet.TurnGreen();
                else
                    ledNet.TurnRed();
            }
            finally
            {
                if (socket != null)
                    socket.Close();
            }
        }
        
        private void connectEthernet()
        {
            if (!ethernet.IsNetworkUp || !ethernet.IsNetworkConnected)
            {
                Debug.Print("Network Up: " + ethernet.IsNetworkUp + "\n" + "Network Connected: " + ethernet.IsNetworkConnected);
                return;
            }
        }
        /// <summary>
        /// Starts to move the pictures from the SD card to the server using the connection created previously.
        /// This method is called when the user tap the upload button on the main window.
        /// </summary>
        public void startUpload()
        {
            mainWindow.Background = new SolidColorBrush(Color.Black);
            display.SimpleGraphics.ClearNoRedraw();
            string text = "Upload images: Please Wait...";
            Font font = Resources.GetFont(Resources.FontResources.myText);
            display.SimpleGraphics.DisplayText(text, font, GT.Color.White, 15, 90);
            ledNet.BlinkRepeatedly(GT.Color.Orange);

            Thread t = new Thread(new ThreadStart(connectUploadSocket));
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();

            uint i = 0;
            while (t.IsAlive)
            {
                if (i > 200)
                {
                    display.SimpleGraphics.ClearNoRedraw();
                    display.SimpleGraphics.DisplayText(text, font, GT.Color.White, 15, 90);
                    i -= 200;
                }
                display.SimpleGraphics.DisplayEllipse(GT.Color.White, 60 + i, 150, 5, 5);
                i += 15;

                Thread.Sleep(100);
            }

            if (ethernet.IsNetworkUp && ethernet.IsNetworkConnected)
                ledNet.TurnGreen();
            else
                ledNet.TurnRed();
            mainWindow.Background = new SolidColorBrush(Color.Black);
            stato = 0;
            ShowInitButtons();
        }
        /// <summary>
        /// Starts to send video streaming to the server using the connection created previously.
        /// This method is called when the user tap the video button on the main window.
        /// </summary>
        public void startVideoStreaming()
        {
            Bitmap tmp = new Bitmap(320, 240);
            Font font = Resources.GetFont(Resources.FontResources.myText);
            string text = "Press Back Button to end";
            tmp.DrawText(text, font, GT.Color.Red, 30, 100);
            mainWindow.Background = new ImageBrush(tmp);

            sending = new Thread(new ThreadStart(connectStreamSocket));
            sending.Priority = ThreadPriority.Normal;
            sending.Start();
        }
        #endregion

        #region DISPLAY Setups
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
            mainWindow.Background = new SolidColorBrush(Color.Black);
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
        #endregion

        #region SDCARD-IMAGE functions
        /// <summary>
        /// Remove the picture actually displayed from the SD card. This method is called when the user tap the trash icon on the screen.
        /// </summary>
        private void removeImage()
        {
            try
            {
                ledSd.BlinkRepeatedly(GT.Color.Orange);
                lock (sdCard)
                {
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
            }
            catch (Exception e)
            {
                mainWindow.Background = new ImageBrush(errorImgBmp);
                Debug.Print(e.StackTrace);
            }
        }
        /// <summary>
        /// Save the last picture taken whit the camera in the SD card if it is correctly mounted.
        /// </summary>
        private void savePicture()
        {
            lock (sdCard)
            {
                if (VerifySDCard())
                {
                    try
                    {
                        ledSd.BlinkRepeatedly(GT.Color.Blue);
                        GT.StorageDevice storage = sdCard.GetStorageDevice();
                        if (storage == null)
                            throw new Exception();
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
                        Debug.Print("Message: " + ex.Message + "  Inner Exception: " + ex.InnerException);
                    }
                    finally
                    {
                        ledSd.TurnOff();
                    }
                }
            }
        }
        /// <summary>
        /// Verify if the SD card is mounted.
        /// </summary>
        /// <returns>(bool) true if the SD card is mounted, false otherwise.</returns>
        public bool VerifySDCard()
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
        /// <summary>
        /// Shows the photo gallery on the display if there is a SD card mounted, otherwise shows a message "Insert SD card".
        /// This method is called when the user tap the gallery button on the display. 
        /// </summary>
        public void mostraGalleria()
        {
            lock (sdCard)
            {
                //mostra la galleria delle immagini salvate
                if (VerifySDCard())
                {
                    try
                    {
                        GT.StorageDevice storage = sdCard.GetStorageDevice();
                        if (storage == null)
                            throw new Exception();
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
        #endregion

        private Color chooseColor(int j)
        {
            if (j < 20)
                return Colors.Red;
            if (j < 40)
                return Colors.Orange;
            if (j < 60)
                return Colors.Yellow;
            if (j < 80)
                return Colors.Green;
            if (j < 100)
                return Colors.Blue;
            if (j < 120)
                return Colors.Cyan;
            if (j < 140)
                return Colors.Purple;
            return Colors.Magenta;
        }

    }
    /// <summary>
    /// Internal class used to send frame by frame the video streaming.
    /// </summary>
    class MyBitmap
    {
        public Bitmap bitmap;
        public bool last;

        /// <summary>
        /// MyBitmap class constructor
        /// </summary>
        /// <param name="last">(bool) if true the bitmap associated is the last of the video stream.</param>
        /// <param name="bmp">Bitmap that represents a frame of the video stream. </param>
        public MyBitmap(bool last, Bitmap bmp)
        {
            this.bitmap = bmp;
            this.last = last;
        }
    }
}
