using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Web;
using System.Net;


namespace Server
{

    public class Emailer
    {

        /// <summary>
        /// Transmit an email message to a recipient without
        /// any attachments
        /// </summary>
        /// <param name="sendTo">Recipient Email Address</param>
        /// <param name="sendFrom">Sender Email Address</param>
        /// <param name="sendSubject">Subject Line Describing Message</param>
        /// <param name="sendMessage">The Email Message Body</param>
        /// <returns>Status Message as String</returns>
        public static string SendMessage(string sendTo, string sendFrom, string sendSubject, string sendMessage)
        {
            string str;
            try
            {
                string[] tos = sendTo.Split(';');
                // Create the basic message
                MailMessage message = new MailMessage();
                message.From = new MailAddress(sendFrom);
                foreach(string to in tos){
                    if (to != null && !to.Equals("") && ValidateEmailAddress(to))
                        message.To.Add(to);
                    else
                        return "Invalid recipient email address " + to;
                }
                message.Subject = sendSubject;
                message.Body = sendMessage;

                // create smtp client at mail server location
                SmtpClient client = new SmtpClient("smtp.gmail.com");

                // Add credentials
                client.Credentials = new NetworkCredential("progettoalbertengo", "FEZ6group");
                client.EnableSsl = true;

                // send message
                client.Send(message);

                str = "Message sent to " + sendTo + " at " + DateTime.Now.ToString() + ".";
                return str;
            }
            catch (Exception ex)
            {
                str = ex.Message.ToString();
                return str;
            }
        }


        /// <summary>
        /// Transmit an email message with
        /// attachments
        /// </summary>
        /// <param name="sendTo">Recipient Email Address</param>
        /// <param name="sendFrom">Sender Email Address</param>
        /// <param name="sendSubject">Subject Line Describing Message</param>
        /// <param name="sendMessage">The Email Message Body</param>
        /// <param name="attachments">A string array pointing to the location of each attachment</param>
        /// <returns>Status Message as String</returns>
        public static string SendMessageWithAttachment(string sendTo, string sendFrom, string sendSubject, string sendMessage, ArrayList attachments)
        {
            string str;
            try
            {
                string[] tos = sendTo.Split(';');
                // Create the basic message
                MailMessage message = new MailMessage();
                message.From = new MailAddress(sendFrom);
                foreach(string to in tos){
                    if(to != null && !to.Equals("") && ValidateEmailAddress(to))
                        message.To.Add(to);
                    else
                        return "Invalid recipient email address " + to;
                }
                message.Subject = sendSubject;
                message.Body = sendMessage;

                // The attachments arraylist should point to a file location where
                // the attachment resides - add the attachments to the message
                foreach (string attach in attachments)
                {
                    Attachment attached = new Attachment(attach, MediaTypeNames.Application.Octet);
                    message.Attachments.Add(attached);
                }

                // create smtp client at mail server location
                SmtpClient client = new SmtpClient("smtp.gmail.com");

                // Add credentials
                client.Credentials = new NetworkCredential("progettoalbertengo", "FEZ6group");
                client.EnableSsl = true;
                client.Timeout = Int32.MaxValue;
                // send message
                client.Send(message);

                str = "Message sent to " + sendTo + " at " + DateTime.Now.ToString() + ".";
                return str;
            }
            catch (Exception ex)
            {
                str = ex.Message.ToString();
                return str;
            }
        }



        /// <summary>
        /// Confirm that an email address is valid
        /// in format
        /// </summary>
        /// <param name="emailAddress">Full email address to validate</param>
        /// <returns>True if email address is valid</returns>
        public static bool ValidateEmailAddress(string emailAddress)
        {
            try
            {
                string TextToValidate = emailAddress;
                Regex expression = new Regex(@"\w+@[a-zA-Z_]+?\.[a-zA-Z]{2,3}");

                // test email address with expression
                if (expression.IsMatch(TextToValidate))
                {
                    // is valid email address
                    return true;
                }
                else
                {
                    // is not valid email address
                    return false;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
}
