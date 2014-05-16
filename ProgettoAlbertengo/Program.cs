﻿using System;
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
        StackPanel takePhoto;
        StackPanel videoStreaming;
        StackPanel gallery;

        Image photoImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.photoImg), Bitmap.BitmapImageType.Bmp));
        Image videoImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.videoImg), Bitmap.BitmapImageType.Bmp));
        Image galleryImg = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.galleryImg), Bitmap.BitmapImageType.Bmp));
        Image photoImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.photoImgPressed), Bitmap.BitmapImageType.Bmp));
        Image videoImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.videoImgPressed), Bitmap.BitmapImageType.Bmp));
        Image galleryImgPressed = new Image(new Bitmap(Resources.GetBytes(Resources.BinaryResources.galleryImgPressed), Bitmap.BitmapImageType.Bmp));

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

        Thread sendThread;

        string[] immaginiGalleria;
        int numeroImmagini, indice;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            SetupDisplay();
            SetupNet();

            camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));

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

            new Thread(new ThreadStart(connectEthernet)).Start();
            Debug.Print("Program Started");
            
        }

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

        void ethernet_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Ethernet Network UP");
        }

        private void sendMessage()
        {
            Debug.Print("sendMessage started");
            Socket s = null;
            try
            {
                s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                String ipAddr = "192.168.137.1";
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddr), 13000);
                s.Connect(remoteEP);
                while (true)
                {
                    if (currentBitmapData != null && currentBitmapData.GetBitmap() != null)
                    {
                        Debug.Print("Send image...");
                        Byte[] outputFileBuffer = new Byte[currentBitmapData.Height * currentBitmapData.Width * 3 + 54];
                        Util.BitmapToBMPFile(currentBitmapData.GetBitmap(), currentBitmapData.Width, currentBitmapData.Height, outputFileBuffer);
                        s.Send(outputFileBuffer);
                    }
                    else
                        Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Debug.Print("Si è verificata un'eccezione..." + e.StackTrace);
            }
            finally
            {
                if (s != null)
                    s.Close();
            }
        }

        void ethernet_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Ethernet Network Down");
        }

        void wifi_NetworkUp(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Network UP");
            ledNet.TurnBlue();
            
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            String ipAddr = "127.0.0.1";
            IPEndPoint remoteEP = new IPEndPoint(Dns.GetHostEntry(ipAddr).AddressList[0], 13000);
            s.Connect(remoteEP);
            s.Send(System.Text.UTF8Encoding.UTF8.GetBytes("ciao minchia"));
            s.Close();
            ledNet.TurnOff();
        }

        void wifi_NetworkDown(GTM.Module.NetworkModule sender, GTM.Module.NetworkModule.NetworkState state)
        {
            Debug.Print("Network DOWN");
            //new Thread(new ThreadStart(connect)).Start();
            ledNet.TurnBlue();
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
            GHI.Premium.Net.WiFiNetworkInfo info = new WiFiNetworkInfo();
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
        }

        private void sdCard_Mounted(SDCard sender, GT.StorageDevice SDCard)
        {
            ledSd.TurnGreen();
            if (stato == 3)
                mostraGalleria();
        }

        private void sdCard_Unmounted(SDCard sender)
        {
            if (!sdCard.IsCardInserted)
                ledSd.TurnRed();
            if (!sdCard.IsCardMounted)
                sdCard.MountSDCard();
        }

        private void camera_BitmapStreamed(Camera sender, Bitmap bitmap)
        {
            if(stato == 1)
                display.SimpleGraphics.DisplayImage(bitmap, 0, 0);
            currentBitmapData = bitmap;
        }

        private void backButton_Pressed(Button sender, Button.ButtonState state)
        {
            Debug.Print("Back Button pressed. State: " + stato);
            switch (stato)
            {
                case 1:
                    // Take Photo state
                    camera.StopStreamingBitmaps();
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
                    mainWindow.Background = new SolidColorBrush(Color.Black);
                    sendThread.Abort();
                    stato = 0;
                    ShowInitButtons();
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
                    new Thread(savePicture).Start();
                    ShowInitButtons();
                    stato = 0;
                    break;
            }
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
            takePhoto = new StackPanel(Orientation.Horizontal);
            videoStreaming = new StackPanel(Orientation.Vertical);
            gallery = new StackPanel(Orientation.Horizontal);


            //Set Margin to 6 for each StackPanel
            initPanel.SetMargin(6);
            takePhoto.SetMargin(6);
            videoStreaming.SetMargin(6);
            gallery.SetMargin(6);
            //Center align StackPanel Horizontally
            takePhoto.HorizontalAlignment = HorizontalAlignment.Center;
            videoStreaming.HorizontalAlignment = HorizontalAlignment.Center;
            gallery.HorizontalAlignment = HorizontalAlignment.Center;
            //Create Associations between StackPanels and Images
            takePhoto.Children.Add(photoImg);
            videoStreaming.Children.Add(videoImg);
            gallery.Children.Add(galleryImg);
            initPanel.Children.Add(takePhoto);
            initPanel.Children.Add(videoStreaming);
            initPanel.Children.Add(gallery);

            takePhoto.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(takePhoto_TouchDown);
            videoStreaming.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(videoStreaming_TouchDown);
            gallery.TouchDown += new Microsoft.SPOT.Input.TouchEventHandler(gallery_TouchDown);

            takePhoto.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(takePhoto_TouchUp);
            videoStreaming.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(videoStreaming_TouchUp);
            gallery.TouchUp += new Microsoft.SPOT.Input.TouchEventHandler(gallery_TouchUp);
        }

        void mainWindow_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            switch (stato)
            {
                case 1:
                    stato = 2;
                    camera.StopStreamingBitmaps();
                    ShowSavePhotoButtons();
                    break;
            }
        }

        void HideInitButtons()
        {
            mainWindow.Child = new StackPanel();
            mainWindow.Invalidate();
        }

        void ShowInitButtons()
        {
            mainWindow.Child = initPanel;
            mainWindow.Invalidate();
        }

        void HideSavePhotoButtons()
        {
            mainWindow.Child = new StackPanel();
            mainWindow.Background = new SolidColorBrush(Color.Black);
            mainWindow.Invalidate();
        }

        void ShowSavePhotoButtons()
        {
            mainWindow.Child = savePhotoPanel;
            mainWindow.Background = new ImageBrush(currentBitmapData);
            mainWindow.Invalidate();
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
            startVideoStreaming();
        }

        private void startVideoStreaming()
        {
            Bitmap tmp = new Bitmap(320, 240);
            tmp.DrawText("Press Back Button to end", Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Red, 50, 100);
            mainWindow.Background = new ImageBrush(tmp);
            sendThread = new Thread(new ThreadStart(sendMessage));
            sendThread.Start();
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
            camera.StartStreamingBitmaps(new Bitmap(camera.CurrentPictureResolution.Width, camera.CurrentPictureResolution.Height));
            
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
            
            new Thread(savePicture).Start(); 
            stato = 0;
            ShowInitButtons();
        }

        void trashPanel_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            trashPanel.Children.Clear();
            trashPanel.Children.Add(fullTrash);
            mainWindow.Invalidate();
        }

        void trashPanel_TouchUp(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            trashPanel.Children.Clear();
            trashPanel.Children.Add(emptyTrash);
            mainWindow.Invalidate();
            removeImage();
            mostraGalleria();
        }

        private void removeImage()
        {
            ledSd.BlinkRepeatedly(GT.Color.Orange);
            sdCard.GetStorageDevice().Delete("\\SD\\Image-" + indice + ".jpg");
            if (numeroImmagini != 1 && indice != (numeroImmagini-1))
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

        void savePicture()
        {
            if (VerifySDCard())
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

                try
                {
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
        
        bool VerifySDCard()
        {
            if (sdCard.IsCardInserted)
            {
                if (!sdCard.IsCardMounted)
                {
                    try
                    {
                        sdCard.MountSDCard();
                      
                        return true;
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
        
        private void confirmSave_TouchDown(object sender, Microsoft.SPOT.Input.TouchEventArgs e)
        {
            confirmSave.Children.Clear();
            confirmSave.Children.Add(confirmSaveImgPressed);
            mainWindow.Invalidate();
        }

        private void mostraGalleria()
        {
            //mostra la galleria delle immagini salvate
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
            else
            {
                mainWindow.Background = new ImageBrush(sdBmpImage);
                Debug.Print("Nessuna SD...");
            }
        }

        void timer_Tick(GT.Timer timer)
        {
            var pos = joystick.GetPosition();
            if (stato == 3)
            {
                if (VerifySDCard()  && numeroImmagini != 0)
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
                            indice= (int)numeroImmagini;
                        mainWindow.Background = new ImageBrush(new GT.Picture(sdCard.GetStorageDevice().ReadFile("\\" + immaginiGalleria[--indice]), GT.Picture.PictureEncoding.BMP).MakeBitmap());
                    }
                    //Debug.Print("Showing image N°: " + showIndex);
                }
            }
        }
    }
}