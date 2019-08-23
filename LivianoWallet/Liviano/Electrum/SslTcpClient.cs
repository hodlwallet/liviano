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
            return false;
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
            SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
            );

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

            Debug.WriteLine("[ReadMessage] Reading message from: ", sslStream.ToString());

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
                if (messageData.ToString().IndexOf("<EOF>", System.StringComparison.CurrentCulture) != -1 ||
                    CanParseToJson(messageData.ToString()))
                {
                    break;
                }
            }

            var msg = messageData.ToString();

            Debug.WriteLine("[ReadMessage] Read message {0}", msg);

            return msg;
        }

        /// <summary>
        /// Example how to use the library
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="port"></param>
        public static void RunClientExample(string serverName, int port = 443)
        {
            // Create a TCP/IP client socket.
            // machineName is the host running the server application.
            TcpClient client = new TcpClient(serverName, port);

            Debug.WriteLine($"[RunClientExample] Connected to: {serverName}:{port}");

            using (var sslStream = GetSslStream(client, serverName))
            {
                // Encode a test message into a byte array.
                // Signal the end of the message using the "<EOF>".
                byte[] messsage = Encoding.UTF8.GetBytes(
                    @"{""id"": ""1"", ""method"": ""server.version"", ""params"": [""HODL"", ""1.4""]}"
                );

                // Send version message to the server. 
                sslStream.Write(messsage);
                sslStream.Flush();

                // Read message from the server.
                string serverMessage = ReadMessage(sslStream);
                Debug.WriteLine("[RunClientExample] Response: {0}", serverMessage);
            }

            // Close the client connection.
            client.Close();
            Debug.WriteLine("[RunClientExample] Client closed.");
        }

        static bool CanParseToJson(string message)
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

            return true;
        }
    }
}
