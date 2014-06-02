using Server;
using Server.MyImageDataSetTableAdapters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    class PanelItem : Panel
    {
        public CheckBox checkBox1 {get; private set;}
        public Label label1 { get; private set; }
        public Button button1 { get; private set; }
        private string path;
        private Image bitmap;

        public PanelItem()
        {
            InitializeComponent();
        }

        public PanelItem(string text, Image bmp) : base()
        {
            path = text;
            bitmap = bmp;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PanelItem));
            this.button1 = new System.Windows.Forms.Button();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.button1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("button1.BackgroundImage")));
            this.button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button1.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.button1.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.button1.Location = new System.Drawing.Point(75, 48);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(60, 60);
            this.button1.TabIndex = 0;
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Visible = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            this.button1.MouseEnter += new System.EventHandler(this.showButton);
            this.button1.MouseLeave += new System.EventHandler(this.showButton);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.checkBox1.Location = new System.Drawing.Point(5, 5);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(22, 21);
            this.checkBox1.TabIndex = 0;
            this.checkBox1.UseVisualStyleBackColor = false;
            this.checkBox1.MouseEnter += new System.EventHandler(this.showButton);
            this.checkBox1.MouseLeave += new System.EventHandler(this.hideButton);
            this.checkBox1.Tag = path;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.label1.Font = new System.Drawing.Font("Segoe Script", 7F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.Red;
            this.label1.Location = new System.Drawing.Point(0, 123);
            this.label1.Margin = new System.Windows.Forms.Padding(3);
            this.label1.MaximumSize = new System.Drawing.Size(204, 30);
            this.label1.MinimumSize = new System.Drawing.Size(204, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(204, 30);
            this.label1.TabIndex = 0;
            this.label1.Text = path;
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.label1.MouseEnter += new System.EventHandler(this.showButton);
            this.label1.MouseLeave += new System.EventHandler(this.hideButton);
            // 
            // PanelItem
            // 
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.BackgroundImage = bitmap;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button1);
            this.Size = new System.Drawing.Size(210, 156);
            this.MouseEnter += new System.EventHandler(this.showButton);
            this.MouseLeave += new System.EventHandler(this.hideButton);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void showButton(object sender, EventArgs e)
        {
            button1.Show();
        }

        private void hideButton(object sender, EventArgs e)
        {
            Point p = this.PointToClient(MousePosition);
            int mouseX = p.X;
            int mouseY = p.Y;
            if(mouseX <= 0 || mouseX >= this.Width || mouseY <= 0 || mouseY >= this.Height)
                button1.Hide();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (path.Contains("Image"))
            {
                ImagesTableAdapter ita = new ImagesTableAdapter();
                MyImageDataSet.ImagesDataTable idt = ita.GetImageById(long.Parse(path.Replace("Image: ", "")));

                var formPicture = new Form();
                var picture = new PictureBox();
                picture.Dock = DockStyle.Fill;
                picture.Image = new Bitmap(Form1.byteArrayToImage((byte[])(idt.Rows[0].ItemArray[1])));
                picture.SizeMode = PictureBoxSizeMode.StretchImage;
                formPicture.Controls.Add(picture);
                formPicture.MaximumSize = new Size(960, 720);
                formPicture.MinimumSize = new Size(960, 720);
                formPicture.MaximizeBox = false;
                formPicture.MinimizeBox = false;
                formPicture.ShowDialog();
                formPicture.Dispose();
            }
            else
                System.Diagnostics.Process.Start(path.Replace("Video: ", ".\\Video\\").Replace("bmp", "avi"));
        }


    }
}
