//
// SslTcpClient.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Liviano.Electrum
{
    public static class SslTcpClient
    {
        public static EventHandler<string> OnSubscriptionMessageEvent;

        /// <summary>
        /// The following method is invoked by the RemoteCertificateValidationDelegate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // The remote certificate is valid!
            if (sslPolicyErrors == SslPolicyErrors.None) return true;

            // If there is more than one error then it shouldn't be allowed
            if (chain.ChainStatus.Length == 1)
            {
                // Self signed certificates have the issuer in the subject field
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors || certificate.Subject == certificate.Issuer)
                {
                    // If THIS is the cause of of the error then allow the certificate, a static 0 as the index is safe given chain.ChainStatus.Length == 1.
                    if (chain.ChainStatus[0].Status == X509ChainStatusFlags.UntrustedRoot)
                    {
                        Debug.WriteLine("[ValidateServerCertificate] Self-signed ssl certificate");

                        // Self-signed certificates with an untrusted root are valid.
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("[ValidateServerCertificate] Error: Failed to validate ssl certificate");
                    }
                }
            }

            Debug.WriteLine("[ValidateServerCertificate] Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return true;
        }

        /// <summary>
        /// Gets an ssl stream from a tcp client of the server, names must match the cert name
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serverName"></param>
        /// <returns></returns>
        public static SslStream GetSslStream(TcpClient client, string serverName)
        {
            // Create an SSL stream that will close the client's stream.
#pragma warning disable IDE0068 // Use recommended dispose pattern
            var sslStream = new SslStream(
                 client.GetStream(),
                 true,
                 new RemoteCertificateValidationCallback(ValidateServerCertificate),
                 null
            );
#pragma warning restore IDE0068 // Use recommended dispose pattern

            Debug.WriteLine("[GetSslStream] Client connected via ssl.");

            // The server name must match the name on the server certificate.
            try
            {
                // TODO move this somewhere else
                sslStream.AuthenticateAsClient(serverName);
            }
            catch (AuthenticationException e)
            {
                Debug.WriteLine($"[GetSslStream] Exception: {e.Message}");

                if (e.InnerException != null)
                {
                    Debug.WriteLine($"[GetSslStream] Inner exception: {e.InnerException.Message}");
                }

                Debug.WriteLine("[GetSslStream] Authentication failed - Closing the connection.");

                client.Close();

                return null;
            }

            sslStream.ReadTimeout = Timeout.Infinite;
            sslStream.WriteTimeout = Timeout.Infinite;

            return sslStream;
        }

        /// <summary>
        /// Reads a message from the SSL Stream on subscription mode.
        /// </summary>
        /// <param name="sslStream"></param>
        public static void ReadSubscriptionMessages(SslStream sslStream)
        {
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();

            int bytes = -1;
            string[] msgs;
            while (bytes != 0)
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);


                // This API supports mutiple messages sent separated by \n
                msgs = GetMessages(messageData.ToString().Trim());

                foreach (var msg in msgs)
                {
                    if (msg.IndexOf("\n", StringComparison.CurrentCulture) != -1)
                        OnSubscriptionMessageEvent?.Invoke(sslStream, msg);
                }
            }
        }

        /// <summary>
        /// Reads a message from the SSL Stream.
        /// </summary>
        /// <param name="sslStream"></param>
        /// <returns></returns>
        public static string ReadMessage(SslStream sslStream)
        {
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();

            int bytes = -1;
            while (bytes != 0)
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);

                // Check for EOF or \n
                if (messageData.ToString().IndexOf("\n", StringComparison.CurrentCulture) != -1 && CanParseToJson(messageData.ToString()))
                    break;
            }

            var msg = messageData.ToString();

            return msg;
        }

        public static string[] GetMessages(string message)
        {
            return message.Split('\n');
        }

        public static bool CanParseToJson(string message)
        {
            message = message.Replace("\n", string.Empty);
            message = message.Replace("\r", string.Empty);
            message = message.Replace("\t", string.Empty);

            var messageArray = message.ToCharArray();
            Array.Reverse(messageArray);
            var reverseMessage = new string(messageArray);

            return message.IndexOf("{", StringComparison.CurrentCulture) == 0 && reverseMessage.IndexOf("}", StringComparison.CurrentCulture) == 0;
        }

        public static async Task ReadMessagesFrom(SslStream stream, Action<string> messageCallback)
        {
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            //byte[] buffer = new byte[2048]; // TODO investigate why this doesn't work
            byte[] buffer = new byte[400000 + 78]; // Max size of a transaction + size of json structure
            StringBuilder messageData = new StringBuilder();

            int bytes = -1;
            while (bytes != 0)
            {

                bytes = await stream.ReadAsync(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);

                // Check for EOF or \n
                if (messageData.ToString().IndexOf("\n", StringComparison.CurrentCulture) != -1 && CanParseToJson(messageData.ToString()))
                    messageCallback(messageData.ToString());
            }
        }
    }
}
