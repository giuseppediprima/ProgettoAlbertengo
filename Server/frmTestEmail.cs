using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace Server
{

    /// <summary>
    /// Test Application Form:
    /// This application is used to test sending
    /// email and email with attachments.
    /// </summary>
    public partial class frmTestEmail : Form
    {

        /// <summary>
        /// An arraylist containing
        /// all of the attachments
        /// </summary>
        ArrayList alAttachments;



        /// <summary>
        /// Default constructor
        /// </summary>
        public frmTestEmail()
        {
            InitializeComponent();
        }

        public frmTestEmail(string from, string subject, string message, string attachment)
        {
            InitializeComponent();
            txtSendFrom.Text = from;
            txtSubjectLine.Text = subject;
            txtMessage.Text = message;
            txtAttachments.Text = attachment;
        }


        /// <summary>
        /// Add files to be attached to the email message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string[] arr = openFileDialog1.FileNames;
                    alAttachments = new ArrayList();
                    txtAttachments.Text = string.Empty;
                    alAttachments.AddRange(arr);

                    foreach (string s in alAttachments)
                    {
                        txtAttachments.Text += s + "; ";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
        }



        /// <summary>
        /// Exit the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            //Application.Exit();
            this.Close();
        }



        /// <summary>
        /// Send an email message with or without attachments
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                result = Emailer.SendMessage(txtSendTo.Text,  txtSendFrom.Text, txtSubjectLine.Text, txtMessage.Text);
            MessageBox.Show(result, "Email Transmittal");

            this.Close();
        }


    }
}
