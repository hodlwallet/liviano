//
// SslTcpClient.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019
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
using System.Collections;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;

using Newtonsoft.Json;

namespace Liviano.Electrum
{
    public static class SslTcpClient
    {
        public static readonly Hashtable _CertificateErrors = new Hashtable();
        public static EventHandler<string> OnSubscriptionMessageEvent;

        /// <summary>
        /// The following method is invoked by the RemoteCertificateValidationDelegate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Debug.WriteLine("[ValidateServerCertificate] Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            // NOTE: Ummm this is debatable in this case, so we return true
            // fuck it.
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
            SslStream sslStream = new SslStream(
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
                sslStream.AuthenticateAsClient(serverName);
            }
            catch (AuthenticationException e)
            {
                Debug.WriteLine("[GetSslStream] Exception: {0}", e.Message);

                if (e.InnerException != null)
                {
                    Debug.WriteLine("[GetSslStream] Inner exception: {0}", e.InnerException.Message);
                }

                Debug.WriteLine("[GetSslStream] Authentication failed - Closing the connection.");

                client.Close();

                return null;
            }

            return sslStream;
        }

        // TODO We need a ReadMessage as below but that doesn't close the connection
        // instead it should keep it open and looping over it waiting on read.
        // this way we can call an event on send respond to it and forget about it
        // this is very useful for the case of a subscription to a block height
        // given a server on the right chain we can then pick up a new one this is untested
        // pseudo code to make it happen I guess...

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

            Debug.WriteLine("[ReadSubscriptionMessage] Reading message from: {0}", sslStream);

            int bytes = -1;
            string msg;
            while (bytes != 0)
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);

                msg = messageData.ToString();
                Debug.WriteLine($"[ReadSubscriptionMessage] Read message {msg.Trim()}");

                if (CanParseToJson(messageData.ToString()))
                    OnSubscriptionMessageEvent?.Invoke(sslStream, msg);
            }

            msg = messageData.ToString();
            Debug.WriteLine($"[ReadSubscriptionMessage] Read message {msg.Trim()}");

            if (CanParseToJson(msg))
                OnSubscriptionMessageEvent?.Invoke(sslStream, msg);
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

            Debug.WriteLine("[ReadMessage] Reading message from: {0}", sslStream);

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

                // Check for EOF && if the message is complete json... Usually this works with electrum
                if (messageData.ToString().IndexOf("<EOF>", StringComparison.CurrentCulture) != -1 ||
                    CanParseToJson(messageData.ToString()))
                {
                    break;
                }
            }

            var msg = messageData.ToString();

            Debug.WriteLine($"[ReadMessage] Read message {msg.Trim()}");

            return msg;
        }

        public static bool CanParseToJson(string message)
        {
            try
            {
                JsonConvert.DeserializeObject(message);
            }
            catch (JsonSerializationException e)
            {
                Debug.WriteLine("[CanParseToJson] Cannot parse to json: ", e.Message);

                return false;
            }
            catch (JsonReaderException e)
            {
                Debug.WriteLine("[CanParseToJson] Cannot parse to json: ", e.Message);

                return false;
            }

            return true;
        }
    }
}
